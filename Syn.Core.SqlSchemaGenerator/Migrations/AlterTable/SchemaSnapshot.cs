using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Migrations.AlterTable;

/// <summary>
/// Represents a snapshot of a table's schema at a specific version.
/// Used for comparing changes between versions.
/// </summary>
public class SchemaSnapshot
{
    /// <summary>
    /// Name of the entity/table.
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the schema snapshot.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// List of columns in the schema.
    /// </summary>
    public List<ColumnDefinition> Columns { get; set; } = new();
}