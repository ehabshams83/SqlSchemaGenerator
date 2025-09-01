namespace Syn.Core.SqlSchemaGenerator;

/// <summary>
/// Represents a structural change detected during migration analysis,
/// such as added, dropped, or modified columns, constraints, or indexes.
/// </summary>
public class ImpactItem
{
    /// <summary>The type of the affected object (e.g., Column, Constraint, Index).</summary>
    public string Type { get; set; }

    /// <summary>The nature of the change (e.g., Added, Dropped, Modified).</summary>
    public string Action { get; set; }

    /// <summary>The name of the table where the change occurred.</summary>
    public string Table { get; set; }

    /// <summary>The name of the affected column, constraint, or index.</summary>
    public string Name { get; set; }

    /// <summary>Optional original SQL type definition before modification.</summary>
    public string? OriginalType { get; set; }

    /// <summary>Optional new SQL type definition after modification.</summary>
    public string? NewType { get; set; }

    /// <summary>Optional severity level of the change (e.g., Low, Medium, High).</summary>
    public string? Severity { get; set; }

    /// <summary>Optional explanation of why this change may be risky or impactful.</summary>
    public string? Reason { get; set; }

    /// <summary>Optional list of affected columns (used for constraints or indexes).</summary>
    public List<string>? AffectedColumns { get; set; }

    /// <summary>
    /// Returns a Markdown-formatted row representing this impact item.
    /// </summary>
    public string ToMarkdownRow() =>
        $"| {Type} | {Action} | {Table} | {Name} | {Severity ?? "-"} | {Reason ?? "-"} |";

    /// <summary>
    /// Returns an HTML-formatted row representing this impact item, with severity-based styling.
    /// </summary>
    public string ToHtmlRow()
    {
        var severityClass = Severity?.ToLowerInvariant() switch
        {
            "high" => "high",
            "medium" => "medium",
            "low" => "low",
            _ => ""
        };

        return $"<tr class='{severityClass}'>" +
               $"<td>{Type}</td><td>{Action}</td><td>{Table}</td><td>{Name}</td>" +
               $"<td>{Severity ?? "-"}</td><td>{Reason ?? "-"}</td></tr>";
    }
}