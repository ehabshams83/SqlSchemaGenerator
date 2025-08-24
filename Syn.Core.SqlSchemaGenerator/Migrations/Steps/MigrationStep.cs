namespace Syn.Core.SqlSchemaGenerator.Migrations.Steps;

/// <summary>
/// Represents a single step in a schema migration process.
/// </summary>
public class MigrationStep
{
    /// <summary>
    /// The type of migration operation (e.g., AddColumn, DropColumn, AlterColumn, AddIndex).
    /// </summary>
    public MigrationOperation Operation { get; set; }

    /// <summary>
    /// The name of the affected entity (table).
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// The schema the entity belongs to.
    /// </summary>
    public string Schema { get; set; } = "dbo";

    /// <summary>
    /// The name of the affected column, if applicable.
    /// </summary>
    public string? ColumnName { get; set; }

    /// <summary>
    /// Optional SQL fragment or full statement representing the change.
    /// </summary>
    public string? Sql { get; set; }

    /// <summary>
    /// Optional description or comment for the step.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional metadata for advanced scenarios (e.g., rollback, dialect-specific info).
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}