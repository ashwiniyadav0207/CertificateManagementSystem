using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CertificateEngine.CaTool;

internal static class Program
{
    private const string InitCommand = "init";
    private const string HelpFlagLong = "--help";
    private const string HelpFlagShort = "-h";

    private const string OrganizationOption = "--organization";
    private const string OutOption = "--out";
    private const string RootPasswordOption = "--root-password";
    private const string SigningPasswordOption = "--signing-password";
    private const string RootYearsOption = "--root-years";
    private const string SigningYearsOption = "--signing-years";
    private const string ForceOption = "--force";

    private const int DefaultRootYears = 20;
    private const int DefaultSigningYears = 2;

    private const string RootPasswordEnvironmentVariable = "CA_ROOT_PASSWORD";
    private const string SigningPasswordEnvironmentVariable = "CA_SIGNING_PASSWORD";

    private const string RootCaPemFileName = "root-ca.pem";
    private const string RootCaPfxFileName = "root-ca.pfx";
    private const string SigningPfxFileName = "signing.pfx";
    private const string ReadmeFileName = "README.md";

    private const string DocumentSigningOid = "1.3.6.1.5.5.7.3.36";
    private const string DocumentSigningOidFriendlyName = "Document Signing";

    private static readonly string[] ValueOptions =
    {
        OrganizationOption,
        OutOption,
        RootPasswordOption,
        SigningPasswordOption,
        RootYearsOption,
        SigningYearsOption,
    };

    public static int Main(string[] args)
    {
        var helpRequested = args.Contains(HelpFlagLong, StringComparer.OrdinalIgnoreCase) ||
            args.Contains(HelpFlagShort, StringComparer.OrdinalIgnoreCase);
        var isInit = args.Length > 0 && string.Equals(args[0], InitCommand, StringComparison.OrdinalIgnoreCase);

        if (args.Length == 0 || !isInit || helpRequested)
        {
            PrintUsage();
            return helpRequested ? 0 : 1;
        }

        return RunInitCommand(args);
    }

    private static int RunInitCommand(string[] args)
    {
        var remainingArguments = args.Skip(1).ToArray();

        if (!TryParseArguments(remainingArguments, out var values, out var flags, out var parseError))
        {
            Console.WriteLine(parseError);
            Console.WriteLine();
            PrintUsage();
            return 1;
        }

        if (!values.TryGetValue(OrganizationOption, out var organization))
        {
            Console.WriteLine("Error: --organization is required.");
            Console.WriteLine();
            PrintUsage();
            return 1;
        }

        if (!values.TryGetValue(OutOption, out var outputDirectory))
        {
            Console.WriteLine("Error: --out is required.");
            Console.WriteLine();
            PrintUsage();
            return 1;
        }

        var rootYears = DefaultRootYears;
        if (values.TryGetValue(RootYearsOption, out var rootYearsRaw) &&
            !TryParsePositiveInt(rootYearsRaw, out rootYears))
        {
            Console.WriteLine($"Error: --root-years must be a positive integer (got '{rootYearsRaw}').");
            Console.WriteLine();
            PrintUsage();
            return 1;
        }

        var signingYears = DefaultSigningYears;
        if (values.TryGetValue(SigningYearsOption, out var signingYearsRaw) &&
            !TryParsePositiveInt(signingYearsRaw, out signingYears))
        {
            Console.WriteLine($"Error: --signing-years must be a positive integer (got '{signingYearsRaw}').");
            Console.WriteLine();
            PrintUsage();
            return 1;
        }

        values.TryGetValue(RootPasswordOption, out var rootPasswordArgument);
        values.TryGetValue(SigningPasswordOption, out var signingPasswordArgument);

        var options = new InitOptions(
            organization,
            outputDirectory,
            rootYears,
            signingYears,
            rootPasswordArgument,
            signingPasswordArgument,
            flags.Contains(ForceOption));

        return ExecuteInit(options);
    }

    private static bool TryParseArguments(
        string[] arguments,
        out Dictionary<string, string> values,
        out HashSet<string> flags,
        out string? error)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            if (string.Equals(argument, ForceOption, StringComparison.OrdinalIgnoreCase))
            {
                flags.Add(ForceOption);
                continue;
            }

            if (ValueOptions.Contains(argument, StringComparer.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Length)
                {
                    error = $"Error: option '{argument}' requires a value.";
                    return false;
                }

                index++;
                values[argument] = arguments[index];
                continue;
            }

            error = $"Error: unrecognized option '{argument}'.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParsePositiveInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result > 0;

    private static int ExecuteInit(InitOptions options)
    {
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var rootPemPath = Path.Combine(outputDirectory, RootCaPemFileName);
        var rootPfxPath = Path.Combine(outputDirectory, RootCaPfxFileName);
        var signingPfxPath = Path.Combine(outputDirectory, SigningPfxFileName);
        var readmePath = Path.Combine(outputDirectory, ReadmeFileName);

        var existingArtifacts = new[] { rootPemPath, rootPfxPath, signingPfxPath }
            .Where(File.Exists)
            .ToArray();

        if (existingArtifacts.Length > 0 && !options.Force)
        {
            Console.WriteLine("Error: the output directory already contains CA material:");
            foreach (var path in existingArtifacts)
            {
                Console.WriteLine($"  - {path}");
            }

            Console.WriteLine();
            Console.WriteLine("Refusing to overwrite existing keys/certificates. Re-run with --force if");
            Console.WriteLine("you really intend to replace them.");
            return 2;
        }

        var (rootPassword, rootPasswordGenerated) =
            ResolvePassword(options.RootPassword, RootPasswordEnvironmentVariable);
        var (signingPassword, signingPasswordGenerated) =
            ResolvePassword(options.SigningPassword, SigningPasswordEnvironmentVariable);

        var (rootCert, signingCert) =
            CreateCertificateAuthority(options.Organization, options.RootYears, options.SigningYears);

        using (rootCert)
        using (signingCert)
        {
            var rootCertPem = rootCert.ExportCertificatePem();
            File.WriteAllText(rootPemPath, rootCertPem + Environment.NewLine);
            File.WriteAllBytes(rootPfxPath, rootCert.Export(X509ContentType.Pfx, rootPassword));

            // CreateSelfSigned() leaves the root certificate's private key attached. Re-load a
            // public-only copy so signing.pfx never embeds the root's private key - only
            // root-ca.pfx should ever contain it.
            using var rootCertPublicOnly = X509Certificate2.CreateFromPem(rootCertPem);
            var signingChain = new X509Certificate2Collection { signingCert, rootCertPublicOnly };
            var signingPfxBytes = signingChain.Export(X509ContentType.Pfx, signingPassword)
                ?? throw new CryptographicException("Failed to export the signing certificate bundle to PFX.");
            File.WriteAllBytes(signingPfxPath, signingPfxBytes);

            File.WriteAllText(readmePath, BuildReadme(options.Organization, options.RootYears, options.SigningYears));

            var writtenPaths = new[] { rootPemPath, rootPfxPath, signingPfxPath, readmePath };
            PrintSummary(rootCert, signingCert, writtenPaths);
            PrintPasswordWarnings(rootPasswordGenerated, rootPassword, signingPasswordGenerated, signingPassword);
        }

        return 0;
    }

    private static (string Password, bool WasGenerated) ResolvePassword(string? cliValue, string environmentVariableName)
    {
        if (!string.IsNullOrEmpty(cliValue))
        {
            return (cliValue, false);
        }

        var environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrEmpty(environmentValue))
        {
            return (environmentValue, false);
        }

        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (generated, true);
    }

    private static (X509Certificate2 RootCert, X509Certificate2 SigningCert) CreateCertificateAuthority(
        string organization, int rootYears, int signingYears)
    {
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Root CA
        using var rootKey = RSA.Create(4096);
        var rootRequest = new CertificateRequest(
            new X500DistinguishedName($"CN={organization} Root CA, O={organization}"),
            rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 0, true));
        rootRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
                true));
        rootRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
        var rootCert = rootRequest.CreateSelfSigned(notBefore, notBefore.AddYears(rootYears));

        // Signing (leaf) cert, issued by the root
        using var signingKey = RSA.Create(3072);
        var signingRequest = new CertificateRequest(
            new X500DistinguishedName($"CN={organization} Certificate Signing, O={organization}"),
            signingKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        signingRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        signingRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        signingRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid(DocumentSigningOid, DocumentSigningOidFriendlyName) }, true));
        signingRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(signingRequest.PublicKey, false));
        var serialNumber = RandomNumberGenerator.GetBytes(16);
        using var signingCertPublicOnly = signingRequest.Create(
            rootCert, notBefore, notBefore.AddYears(signingYears), serialNumber);
        var signingCert = signingCertPublicOnly.CopyWithPrivateKey(signingKey);

        return (rootCert, signingCert);
    }

    private static void PrintUsage()
    {
        string[] lines =
        {
            "CertificateEngine.CaTool - offline, self-hosted Certificate Authority tool",
            string.Empty,
            "Usage:",
            "  CertificateEngine.CaTool init --organization \"<name>\" --out <output-dir>",
            "      [--root-password <pw>] [--signing-password <pw>]",
            "      [--root-years 20] [--signing-years 2] [--force]",
            string.Empty,
            "Commands:",
            "  init                     Generate a new root CA and signing certificate.",
            string.Empty,
            "Required options:",
            "  --organization <name>    Organization name embedded in the certificates.",
            "  --out <dir>              Directory to write the CA material into.",
            string.Empty,
            "Optional options:",
            "  --root-password <pw>     Password for root-ca.pfx. Falls back to the",
            "                           CA_ROOT_PASSWORD environment variable, then an",
            "                           auto-generated random password.",
            "  --signing-password <pw>  Password for signing.pfx. Falls back to the",
            "                           CA_SIGNING_PASSWORD environment variable, then",
            "                           an auto-generated random password.",
            "  --root-years <n>         Root CA validity in years (default: 20).",
            "  --signing-years <n>      Signing certificate validity in years (default: 2).",
            "  --force                  Overwrite existing CA material found in --out.",
            "  --help, -h               Show this help text.",
            string.Empty,
            "Exit codes:",
            "  0   Success.",
            "  1   Invalid, missing, or unrecognized arguments (usage shown above).",
            "  2   --out already contains CA material and --force was not given.",
        };

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
    }

    private static void PrintSummary(X509Certificate2 rootCert, X509Certificate2 signingCert, string[] writtenPaths)
    {
        Console.WriteLine("Certificate authority material generated successfully.");
        Console.WriteLine();
        Console.WriteLine("Root CA certificate:");
        Console.WriteLine($"  Subject:  {rootCert.Subject}");
        Console.WriteLine($"  SHA-256:  {Convert.ToHexString(rootCert.GetCertHash(HashAlgorithmName.SHA256))}");
        Console.WriteLine($"  Valid:    {FormatUtc(rootCert.NotBefore)} to {FormatUtc(rootCert.NotAfter)}");
        Console.WriteLine();
        Console.WriteLine("Signing certificate:");
        Console.WriteLine($"  Subject:  {signingCert.Subject}");
        Console.WriteLine($"  SHA-256:  {Convert.ToHexString(signingCert.GetCertHash(HashAlgorithmName.SHA256))}");
        Console.WriteLine($"  Valid:    {FormatUtc(signingCert.NotBefore)} to {FormatUtc(signingCert.NotAfter)}");
        Console.WriteLine();
        Console.WriteLine("Files written:");
        foreach (var path in writtenPaths)
        {
            Console.WriteLine($"  - {path}");
        }
    }

    private static void PrintPasswordWarnings(
        bool rootPasswordGenerated,
        string rootPassword,
        bool signingPasswordGenerated,
        string signingPassword)
    {
        if (!rootPasswordGenerated && !signingPasswordGenerated)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("!!! ONE-TIME DISPLAY OF AUTO-GENERATED PASSWORD(S) !!!");
        Console.WriteLine("Copy the value(s) below into a password manager RIGHT NOW - they are never");
        Console.WriteLine("written to disk and cannot be recovered later. Afterward, clear your");
        Console.WriteLine("terminal's scrollback/history.");
        Console.WriteLine();

        if (rootPasswordGenerated)
        {
            Console.WriteLine($"  root-ca.pfx password:    {rootPassword}");
        }

        if (signingPasswordGenerated)
        {
            Console.WriteLine($"  signing.pfx password:    {signingPassword}");
        }

        Console.WriteLine();
    }

    private static string FormatUtc(DateTime value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    private static string BuildReadme(string organization, int rootYears, int signingYears)
    {
        string[] lines =
        {
            $"# Certificate Authority Material for {organization}",
            string.Empty,
            "This directory was generated by CertificateEngine.CaTool. It contains the",
            $"cryptographic material {organization} uses to sign PDF certificates.",
            string.Empty,
            "## Files",
            string.Empty,
            "- `root-ca.pem` - The root CA's **public certificate only** (no private key).",
            "  Safe to distribute and publish, for example alongside issued PDF",
            "  certificates or on a public verification page, so anyone can build a trust",
            "  chain back to this root.",
            string.Empty,
            "- `root-ca.pfx` - The root CA certificate **together with its private key**,",
            "  protected by the root password. Extremely sensitive: whoever holds this",
            "  file and its password can mint new signing certificates that will be",
            "  trusted by anything that trusts this root. It is only needed when issuing",
            "  or re-issuing a signing certificate, which should be a rare, deliberate",
            "  event.",
            string.Empty,
            "- `signing.pfx` - The day-to-day signing (leaf) certificate, together with",
            "  its private key and the root certificate in its chain, protected by the",
            "  signing password. This is the file the main application loads to sign PDF",
            "  certificates. It is sensitive, but far less catastrophic to rotate than the",
            "  root.",
            string.Empty,
            "## What is safe to share vs. what must stay secret",
            string.Empty,
            "- Safe to share/publish: `root-ca.pem`",
            "- Must be kept secret: `root-ca.pfx` and `signing.pfx`",
            string.Empty,
            "## Operational recommendations",
            string.Empty,
            "- Move `root-ca.pfx` to offline/cold storage (for example, an encrypted USB",
            "  drive kept in a safe) immediately after generation. The running",
            "  application only ever needs `signing.pfx` day-to-day; it should never need",
            "  the root private key during normal operation.",
            $"- Rotate the signing certificate roughly every {signingYears} year(s) (its",
            "  configured validity period) by re-issuing a new `signing.pfx` from the",
            "  same root, using a future version of this tool, well before it expires.",
            $"- The root certificate was issued with a validity of {rootYears} year(s).",
            "  Plan to generate a new root (and re-distribute `root-ca.pem`) well ahead",
            "  of its expiry.",
            "- For real production use, avoid letting a single person hold the full",
            "  passphrase protecting these files. Consider splitting each passphrase",
            "  among multiple trusted people using a threshold scheme such as Shamir's",
            "  Secret Sharing, so no single individual can unilaterally access the root",
            "  or signing keys.",
        };

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}

internal sealed record InitOptions(
    string Organization,
    string OutputDirectory,
    int RootYears,
    int SigningYears,
    string? RootPassword,
    string? SigningPassword,
    bool Force);
