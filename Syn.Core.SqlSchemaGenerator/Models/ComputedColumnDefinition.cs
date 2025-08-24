namespace Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents a computed column within an entity.
/// Used to define expressions and persistence behavior.
/// </summary>
public class ComputedColumnDefinition
{
    /// <summary>Name of the computed column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SQL data type of the computed column.</summary>
    public string DataType { get; set; } = "nvarchar(max)";

    /// <summary>SQL expression used to compute the column value.</summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>Indicates whether the computed column is persisted in the database.</summary>
    public bool IsPersisted { get; set; }

    /// <summary>Optional description or comment for the computed column.</summary>
    public string? Description { get; set; }

    /// <summary>Optional source of the computed column (e.g., "Attribute", "Manual").</summary>
    public string? Source { get; set; }

    /// <summary>Indicates whether the column should be ignored during generation.</summary>
    public bool IsIgnored { get; set; }

    /// <summary>Optional reason for ignoring the column.</summary>
    public string? IgnoreReason { get; set; }

    /// <summary>Optional order of the column within the table definition.</summary>
    public int? Order { get; set; }
}