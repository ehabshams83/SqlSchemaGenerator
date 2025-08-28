namespace Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents an index definition applied to one or more columns.
/// Supports uniqueness, inclusion, and optional description.
/// </summary>
public class IndexModel
{
    /// <summary>
    /// Optional name of the index. If null, a default name may be generated.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Indicates whether the index enforces uniqueness.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// The list of column names included in the index.
    /// </summary>
    public List<string> IncludeColumns { get; set; } = new();

    /// <summary>
    /// Optional description of the index, used for documentation or extended properties.
    /// </summary>
    public string? Description { get; set; }
    // توسعة جديدة
    public bool IsFullText { get; set; }
    /// <summary>
    /// Optional SQL filter expression applied to the index.
    /// </summary>
    public string? FilterExpression { get; set; }
}