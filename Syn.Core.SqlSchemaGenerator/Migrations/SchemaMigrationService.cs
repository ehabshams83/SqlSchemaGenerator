
using Microsoft.Data.SqlClient;

namespace Syn.Core.SqlSchemaGenerator.Migrations;

/// <summary>
/// Default implementation of ISchemaMigrationService using SQL Server and ADO.NET.
/// </summary>
public class SchemaMigrationService : ISchemaMigrationService
{
    private readonly string _connectionString;

    public SchemaMigrationService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<MigrationResult> ApplyMigrationsAsync(IEnumerable<SqlMigrationScript> scripts)
    {
        await EnsureSchemaHistoryTableExistsAsync();

        var result = new MigrationResult();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var script in scripts)
        {
            if (await HasBeenExecutedAsync(script.Hash, connection))
            {
                result.SkippedScripts.Add(script.EntityName);
                continue;
            }

            using var command = new SqlCommand(script.Sql, connection);
            await command.ExecuteNonQueryAsync();

            await RecordMigrationAsync(script.EntityName, script.Version, script.Hash, connection);
            result.ExecutedScripts.Add(script.EntityName);
        }

        return result;
    }

    public async Task<bool> HasBeenExecutedAsync(string scriptHash)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return await HasBeenExecutedAsync(scriptHash, connection);
    }

    private async Task<bool> HasBeenExecutedAsync(string scriptHash, SqlConnection connection)
    {
        var query = "SELECT COUNT(*) FROM __SchemaHistory WHERE ScriptHash = @Hash";
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Hash", scriptHash);
        var count = (int)await command.ExecuteScalarAsync();
        return count > 0;
    }

    public async Task RecordMigrationAsync(string entityName, string version, string scriptHash)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await RecordMigrationAsync(entityName, version, scriptHash, connection);
    }

    /// <summary>
    /// Ensures that the __SchemaHistory table exists in the database.
    /// Creates it if not found.
    /// </summary>
    public async Task EnsureSchemaHistoryTableExistsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var checkTableQuery = @"
        IF NOT EXISTS (
            SELECT * FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = '__SchemaHistory'
        )
        BEGIN
            CREATE TABLE __SchemaHistory (
                Id INT IDENTITY PRIMARY KEY,
                EntityName NVARCHAR(255),
                Version NVARCHAR(50),
                ScriptHash NVARCHAR(64),
                ExecutedAt DATETIME DEFAULT GETDATE()
            )
        END";

        using var command = new SqlCommand(checkTableQuery, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task RecordMigrationAsync(string entityName, string version, string scriptHash, SqlConnection connection)
    {
        var insert = @"
            INSERT INTO __SchemaHistory (EntityName, Version, ScriptHash, ExecutedAt)
            VALUES (@EntityName, @Version, @Hash, GETDATE())";

        using var command = new SqlCommand(insert, connection);
        command.Parameters.AddWithValue("@EntityName", entityName);
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@Hash", scriptHash);
        await command.ExecuteNonQueryAsync();
    }
}