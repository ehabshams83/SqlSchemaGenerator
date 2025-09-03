using Microsoft.Data.SqlClient;

using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models; // For EntityDefinition

using System.Security.Cryptography;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Storage;

/// <summary>
/// Manages the schema migration history table in the database and persists schema snapshots.
/// Combines EF-style migration tracking with extended metadata such as script hashes,
/// snapshot hashes, execution status, and notes.
/// </summary>
public class MigrationHistoryStore
{
    private readonly string _connectionString;
    private readonly ISchemaSnapshotStore _snapshotStore;
    private readonly MigrationSettings _settings;

    public MigrationHistoryStore(string connectionString, ISchemaSnapshotStore snapshotStore, MigrationSettings settings = null)
    {
        _connectionString = connectionString;
        _snapshotStore = snapshotStore;
        _settings = settings ?? new MigrationSettings();
    }


    /// <summary>
    /// Ensures the migration history table exists and inserts a pending record.
    /// </summary>
    public (bool isNewVersion, string version) EnsureTableAndInsertPending(
        string migrationScript,
        EntityDefinition newEntity,
        string logicalGroup = null)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var createSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SchemaMigrations]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[SchemaMigrations](
        [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Version] NVARCHAR(64) NOT NULL,
        [ProductVersion] NVARCHAR(32) NULL,
        [ScriptName] NVARCHAR(256) NULL,
        [ScriptHash] VARBINARY(32) NULL,
        [SnapshotHash] VARBINARY(32) NULL,
        [AppliedAtUtc] DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        [DurationMs] INT NULL,
        [Status] NVARCHAR(32) NOT NULL,
        [Notes] NVARCHAR(1024) NULL,
        CONSTRAINT UQ_SchemaMigrations_Version UNIQUE ([Version])
    );
END;
";
        using (var cmd = new SqlCommand(createSql, conn))
            cmd.ExecuteNonQuery();

        var version = GenerateMigrationVersion(newEntity, logicalGroup);

        byte[] scriptHash;
        using (var sha = SHA256.Create())
            scriptHash = sha.ComputeHash(Encoding.UTF8.GetBytes(migrationScript));

        var notes = HelperMethod._suppressedWarnings.Any()
            ? string.Join("; ", HelperMethod._suppressedWarnings.Distinct().OrderBy(x => x))
            : null;

        var checkSql = "SELECT COUNT(1) FROM [dbo].[SchemaMigrations] WHERE [Version] = @v";
        using (var cmd = new SqlCommand(checkSql, conn))
        {
            cmd.Parameters.AddWithValue("@v", version);
            var exists = (int)cmd.ExecuteScalar() > 0;
            if (exists)
            {
                Console.WriteLine($"[MIGRATION] Version {version} already exists in history. Skipping insert.");
                return (false, version);
            }
        }

        var insertSql = @"
INSERT INTO [dbo].[SchemaMigrations] ([Version], [ProductVersion], [ScriptName], [ScriptHash], [Status], [Notes])
VALUES (@v, @productVersion, @name, @hash, 'Pending', @notes)";
        using (var cmd = new SqlCommand(insertSql, conn))
        {
            cmd.Parameters.AddWithValue("@v", version);
            cmd.Parameters.AddWithValue("@productVersion", typeof(MigrationHistoryStore).Assembly.GetName().Version?.ToString() ?? "1.0.0");
            cmd.Parameters.AddWithValue("@name", $"{newEntity.Schema}.{newEntity.Name}");
            cmd.Parameters.AddWithValue("@hash", (object)scriptHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        Console.WriteLine($"[MIGRATION] Version {version} inserted as Pending.");
        return (true, version);
    }

    public void MarkApplied(string version, int durationMs, IEnumerable<EntityDefinition> entities, string notes = null)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        _snapshotStore.SaveSnapshot(version, entities.ToList());
        var snapshotHash = _snapshotStore.ComputeSnapshotHash(version);

        var sql = @"
UPDATE [dbo].[SchemaMigrations]
SET [Status] = 'Applied',
    [DurationMs] = @dur,
    [Notes] = @notes,
    [SnapshotHash] = @snapHash
WHERE [Version] = @v";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@v", version);
        cmd.Parameters.AddWithValue("@dur", durationMs);
        cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@snapHash", (object)snapshotHash ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void MarkFailed(string version, string notes = null)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        var sql = @"
UPDATE [dbo].[SchemaMigrations]
SET [Status] = 'Failed',
    [Notes] = @notes
WHERE [Version] = @v";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@v", version);
        cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Generates a migration version string based on the configured mode.
    /// </summary>
    private string GenerateMigrationVersion(EntityDefinition entity = null, string logicalGroup = null)
    {
        var baseVersion = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        return _settings.VersionMode switch
        {
            MigrationVersionMode.SingleBatch => baseVersion,
            MigrationVersionMode.PerEntity => $"{baseVersion}_{entity?.Name}",
            MigrationVersionMode.PerLogicalBatch => $"{baseVersion}_{logicalGroup}",
            _ => baseVersion
        };
    }

}
