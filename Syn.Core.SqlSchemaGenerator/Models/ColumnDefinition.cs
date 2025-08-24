namespace Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents a physical column definition used in SQL generation.
/// Includes type, constraints, indexing, and optional metadata.
/// </summary>
public class ColumnDefinition
{
    /// <summary>
    /// The name of the column.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The SQL type name of the column (e.g., "nvarchar(100)").
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Indicates whether the column allows null values.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// The default value assigned to the column, if any.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Optional collation setting for the column.
    /// </summary>
    public string? Collation { get; set; }

    /// <summary>
    /// Optional description of the column, used for documentation or extended properties.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether the column has a unique constraint.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// The name of the unique constraint, if explicitly defined.
    /// </summary>
    public string? UniqueConstraintName { get; set; }

    /// <summary>
    /// A list of indexes that include this column.
    /// </summary>
    public List<IndexDefinition> Indexes { get; set; } = new();

    /// <summary>
    /// A list of check constraints applied to this column.
    /// </summary>
    public List<CheckConstraintDefinition> CheckConstraints { get; set; } = new();

    /// <summary>
    /// Indicates whether the column should be excluded from SQL generation.
    /// </summary>
    public bool IsIgnored { get; set; }

    /// <summary>
    /// Optional reason for ignoring the column.
    /// </summary>
    public string? IgnoreReason { get; set; }

    /// <summary>
    /// Indicates whether the column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Indicates whether the column is a foreign key.
    /// </summary>
    public bool IsForeignKey { get; set; }

    /// <summary>
    /// The target entity name for the foreign key relationship.
    /// </summary>
    public string? ForeignKeyTarget { get; set; }

    /// <summary>
    /// Optional ordering value used to control column position in generated SQL.
    /// </summary>
    public int? Order { get; set; }
}