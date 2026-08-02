using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CertificateVerification.Web;

public sealed class CertificateLookup(IOptions<VerificationOptions> options)
{
    private readonly VerificationOptions _options = options.Value;

    public async Task<VerifiedCertificate?> FindAsync(string publicId, CancellationToken cancellationToken)
    {
        var dataDirectory = ResolveDataDirectory();
        if (!File.Exists(Path.Combine(dataDirectory, "certificates.db"))) return null;
        var builder = new SqliteConnectionStringBuilder { DataSource = Path.Combine(dataDirectory, "certificates.db"), Mode = SqliteOpenMode.ReadOnly };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT public_id, certificate_number, participant_name, event_name, status, artifact_path, artifact_sha256, issued_at, revoked_at, revocation_reason FROM certificates WHERE public_id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", publicId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var record = new VerificationRecord(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture), reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture), reader.IsDBNull(9) ? null : reader.GetString(9));
        var signatureValid = record.Status == "Issued" && await VerifyArtifactAsync(dataDirectory, record, cancellationToken);
        var status = record.Status == "Revoked" ? "Revoked" : signatureValid ? "Valid" : "Invalid";
        return new VerifiedCertificate(record, status, signatureValid);
    }

    private async Task<bool> VerifyArtifactAsync(string dataDirectory, VerificationRecord record, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(record.ArtifactPath) || string.IsNullOrWhiteSpace(record.ArtifactSha256)) return false;
            var pdfPath = Path.Combine(dataDirectory, record.ArtifactPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(pdfPath)) return false;
            var pdf = await File.ReadAllBytesAsync(pdfPath, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(record.ArtifactSha256), SHA256.HashData(pdf))) return false;
            if (string.IsNullOrWhiteSpace(_options.TrustedRootPath)) return true; // local unsigned test mode only
            var p7sPath = pdfPath + ".p7s";
            if (!File.Exists(p7sPath)) return false;
            var cms = new SignedCms(new ContentInfo(pdf), detached: true);
            cms.Decode(await File.ReadAllBytesAsync(p7sPath, cancellationToken));
            cms.CheckSignature(verifySignatureOnly: true);
            var signer = cms.SignerInfos.Count == 1 ? cms.SignerInfos[0].Certificate : null;
            if (signer is null) return false;
            using var root = X509Certificate2.CreateFromPemFile(ResolvePath(_options.TrustedRootPath));
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.ExtraStore.AddRange(cms.Certificates);
            return chain.Build(signer);
        }
        catch (Exception exception) when (exception is CryptographicException
            or FormatException
            or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ResolvePath(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(path, AppContext.BaseDirectory);

    private string ResolveDataDirectory()
    {
        if (!string.Equals(_options.DataDirectory, "data", StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePath(_options.DataDirectory);
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data"),
            Path.GetFullPath("../certificate-engine/data", AppContext.BaseDirectory),
            Path.GetFullPath("../../../../CertificateEngine/bin/Release/net8.0/data", AppContext.BaseDirectory),
            Path.Combine(Directory.GetCurrentDirectory(), "data")
        };
        return candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(candidate, "certificates.db")))
            ?? candidates[0];
    }
}

public sealed record VerificationRecord(string PublicId, string CertificateNumber, string ParticipantName, string EventName, string Status, string? ArtifactPath, string? ArtifactSha256, DateTimeOffset? IssuedAt, DateTimeOffset? RevokedAt, string? RevocationReason);
public sealed record VerifiedCertificate(VerificationRecord Record, string Status, bool SignatureValid);
