using Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents a logical entity definition used for schema generation and comparison.
/// Includes metadata such as name, schema, version, columns, constraints, and tags.
/// </summary>
public class EntityModel
{
    /// <summary>
    /// The name of the entity (e.g., table or view name).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The schema name under which the entity resides.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// The version identifier for the entity, used in migration tracking.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Optional description of the entity, used for documentation or extended properties.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The list of columns defined within the entity.
    /// </summary>
    public List<ColumnModel> Columns { get; set; } = new();

    /// <summary>
    /// A list of constraint identifiers applied to the entity.
    /// </summary>
    public List<string> Constraints { get; set; } = new();

    /// <summary>
    /// A list of table-level indexes defined for the entity.
    /// </summary>
    public List<IndexModel> TableIndexes { get; set; } = new();

    /// <summary>
    /// A list of computed columns defined within the entity.
    /// </summary>
    public List<ColumnModel> ComputedColumns { get; set; } = new();

    /// <summary>
    /// Indicates whether the entity should be excluded from schema generation.
    /// </summary>
    public bool IsIgnored { get; set; }

    /// <summary>
    /// Optional source identifier from which the entity was derived.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// A list of tags associated with the entity, used for classification or filtering.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Indicates whether the entity represents a SQL view rather than a table.
    /// </summary>
    public bool IsView { get; set; }
}