using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using CertificateEngine.Configuration;

namespace CertificateEngine.Infrastructure.Persistence;

public sealed class CertificateDatabase
{
    private readonly string _connectionString;

    public CertificateDatabase(IOptions<PlatformOptions> options)
    {
        // Resolve relative runtime data beside the process working directory, not the
        // single-file extraction directory. This keeps the engine and verifier's shared
        // data path predictable when distributed as standalone executables.
        if (Path.IsPathRooted(options.Value.DataDirectory))
        {
            DataDirectory = options.Value.DataDirectory;
        }
        else
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine("..", "..", options.Value.DataDirectory)),
                Path.GetFullPath(Path.Combine("..", options.Value.DataDirectory)),
                Path.GetFullPath(options.Value.DataDirectory)
            };
            DataDirectory = candidates.FirstOrDefault(Directory.Exists) ?? candidates.Last();
        }
        Directory.CreateDirectory(DataDirectory);

        var databasePath = Path.Combine(DataDirectory, "certificates.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = true,
            DefaultTimeout = 10
        }.ToString();
    }

    public string DataDirectory { get; }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 10000;

            CREATE TABLE IF NOT EXISTS certificates (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                issuance_key TEXT NOT NULL UNIQUE,
                public_id TEXT NOT NULL UNIQUE,
                certificate_number TEXT NOT NULL UNIQUE,
                event_id TEXT NOT NULL,
                event_name TEXT NOT NULL,
                source_id TEXT NOT NULL,
                source_hash TEXT NOT NULL,
                participant_name TEXT NOT NULL,
                participant_email TEXT NULL,
                participant_phone TEXT NULL,
                template_id TEXT NOT NULL,
                source_row_number INTEGER NOT NULL,
                status TEXT NOT NULL,
                artifact_path TEXT NULL,
                artifact_sha256 TEXT NULL,
                signer_thumbprint TEXT NULL,
                created_at TEXT NOT NULL,
                issued_at TEXT NULL,
                revoked_at TEXT NULL,
                revocation_reason TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_certificates_event_source
                ON certificates(event_id, source_id);

            CREATE TABLE IF NOT EXISTS deliveries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                certificate_id INTEGER NOT NULL,
                channel TEXT NOT NULL,
                destination TEXT NOT NULL,
                status TEXT NOT NULL,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                next_attempt_at TEXT NOT NULL,
                provider_message_id TEXT NULL,
                last_error TEXT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY(certificate_id) REFERENCES certificates(id),
                UNIQUE(certificate_id, channel, destination)
            );

            CREATE INDEX IF NOT EXISTS ix_deliveries_due
                ON deliveries(status, next_attempt_at);

            CREATE TABLE IF NOT EXISTS audit_events (
                sequence INTEGER PRIMARY KEY,
                previous_hash TEXT NOT NULL,
                event_hash TEXT NOT NULL UNIQUE,
                occurred_at TEXT NOT NULL,
                event_type TEXT NOT NULL,
                entity_id TEXT NOT NULL,
                payload TEXT NOT NULL
            );

            CREATE TRIGGER IF NOT EXISTS audit_events_no_update
            BEFORE UPDATE ON audit_events
            BEGIN
                SELECT RAISE(ABORT, 'audit events are append-only');
            END;

            CREATE TRIGGER IF NOT EXISTS audit_events_no_delete
            BEFORE DELETE ON audit_events
            BEGIN
                SELECT RAISE(ABORT, 'audit events are append-only');
            END;

            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                email TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                role TEXT NOT NULL DEFAULT 'admin',
                created_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
