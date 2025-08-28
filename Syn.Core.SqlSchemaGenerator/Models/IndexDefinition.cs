namespace Syn.Core.SqlSchemaGenerator.Models;
/// <summary>
/// Represents a physical index definition used in SQL generation.
/// Includes column list, uniqueness, filter expression, and optional description.
/// </summary>
public class IndexDefinition
{
    /// <summary>
    /// The name of the index.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The list of column names included in the index.
    /// </summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>
    /// Indicates whether the index enforces uniqueness.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Optional SQL filter expression applied to the index.
    /// </summary>
    public string? FilterExpression { get; set; }

    /// <summary>
    /// Optional description of the index, used for documentation or extended properties.
    /// </summary>
    public string? Description { get; set; }

    // توسعة جديدة
    public bool IsFullText { get; set; }
    public List<string>? IncludeColumns { get; set; }

}