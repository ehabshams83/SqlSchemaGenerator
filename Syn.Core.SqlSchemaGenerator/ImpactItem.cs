namespace Syn.Core.SqlSchemaGenerator;

/// <summary>
/// Represents a structural change detected during migration analysis,
/// such as added, dropped, or modified columns, constraints, or indexes.
/// </summary>
public class ImpactItem
{
    /// <summary>
    /// The type of the affected object: "Column", "Constraint", "Index", etc.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// The nature of the change: "Added", "Dropped", "Modified".
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// The name of the table where the change occurred.
    /// </summary>
    public string Table { get; set; }

    /// <summary>
    /// The name of the affected column, constraint, or index.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The original SQL type definition (e.g., "nvarchar(100) NOT NULL") before modification.
    /// </summary>
    public string? OriginalType { get; set; }

    /// <summary>
    /// The new SQL type definition after modification.
    /// </summary>
    public string? NewType { get; set; }

    /// <summary>
    /// Optional severity level of the change: "Low", "Medium", "High".
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// Optional explanation of why this change may be risky or impactful.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Optional list of affected columns (used for constraints or indexes).
    /// </summary>
    public List<string>? AffectedColumns { get; set; }
}
