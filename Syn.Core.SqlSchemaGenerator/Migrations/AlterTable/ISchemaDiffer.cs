namespace Syn.Core.SqlSchemaGenerator.Migrations.AlterTable;

/// <summary>
/// Defines the contract for comparing two schema snapshots and generating ALTER TABLE scripts.
/// </summary>
public interface ISchemaDiffer
{
    /// <summary>
    /// Compares two schema snapshots and returns the necessary ALTER TABLE statements.
    /// </summary>
    List<string> GenerateAlterTableScripts(SchemaSnapshot oldSchema, SchemaSnapshot newSchema);
}