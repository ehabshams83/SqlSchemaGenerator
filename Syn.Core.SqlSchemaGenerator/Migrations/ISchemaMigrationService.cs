namespace Syn.Core.SqlSchemaGenerator.Migrations;

/// <summary>
/// Defines the contract for applying and tracking schema migration scripts.
/// </summary>
public interface ISchemaMigrationService
{
    Task<MigrationResult> ApplyMigrationsAsync(IEnumerable<SqlMigrationScript> scripts);
    Task<bool> HasBeenExecutedAsync(string scriptHash);
    Task RecordMigrationAsync(string entityName, string version, string scriptHash);
}