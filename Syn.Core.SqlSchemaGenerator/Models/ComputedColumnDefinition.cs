namespace Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents a computed column definition used in SQL generation.
/// Includes expression, persistence, description, and optional metadata.
/// </summary>
public class ComputedColumnDefinition
{
    /// <summary>
    /// The name of the computed column.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The SQL data type of the computed column.
    /// </summary>
    public string DataType { get; set; }

    /// <summary>
    /// The SQL expression used to compute the column value.
    /// </summary>
    public string Expression { get; set; }

    /// <summary>
    /// Indicates whether the computed column is persisted in the database.
    /// </summary>
    public bool IsPersisted { get; set; }

    /// <summary>
    /// Optional description of the computed column, used for documentation or extended properties.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional source property name from which the column was derived.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Indicates whether the column should be excluded from SQL generation.
    /// </summary>
    public bool IsIgnored { get; set; }

    /// <summary>
    /// Optional reason for ignoring the column.
    /// </summary>
    public string? IgnoreReason { get; set; }

    /// <summary>
    /// Optional ordering value used to control column position in generated SQL.
    /// </summary>
    public int? Order { get; set; }

    /// <summary>
    /// Indicates whether the computed expression is indexable (e.g., uses LEN, UPPER, DATEPART).
    /// Used to auto-generate indexes on computed columns.
    /// </summary>
    public bool IsIndexable { get; set; }
}