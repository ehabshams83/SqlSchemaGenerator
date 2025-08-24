namespace Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents the schema definition of a database entity (table or view).
/// Contains metadata for table name, schema, columns, constraints, and indexes.
/// </summary>
public class EntityModel
{
    /// <summary>The name of the entity or table.</summary>
    public string Name { get; set; } = null!;

    /// <summary>The database schema name (e.g., dbo, public).</summary>
    public string Schema { get; set; } = "dbo";

    /// <summary>The version of the entity schema, used for migration tracking.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Optional description of the entity for documentation purposes.</summary>
    public string? Description { get; set; }

    /// <summary>The list of columns defined in the entity.</summary>
    public List<ColumnModel> Columns { get; set; } = new();

    /// <summary>Optional list of constraints (e.g., primary keys, foreign keys).</summary>
    public List<string> Constraints { get; set; } = new();

    /// <summary>Optional list of indexes defined at the table level.</summary>
    public List<IndexModel> TableIndexes { get; set; } = new();

    /// <summary>Optional list of computed columns, if separated from regular columns.</summary>
    public List<ColumnModel> ComputedColumns { get; set; } = new();

    /// <summary>Indicates whether this entity should be ignored during generation.</summary>
    public bool IsIgnored { get; set; } = false;

    /// <summary>Optional source of the entity definition (e.g., "Attribute", "ExternalFile").</summary>
    public string? Source { get; set; }

    /// <summary>Optional tags used to classify the entity (e.g., "Audit", "Reference").</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Indicates whether the entity represents a database view instead of a table.</summary>
    public bool IsView { get; set; } = false;
}