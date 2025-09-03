namespace Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents a physical column definition used in SQL generation.
/// Includes type, constraints, indexing, computed logic, and optional metadata.
/// </summary>
public class ColumnDefinition
{
    // 🔹 Core Properties

    /// <summary>The name of the column.</summary>
    public string Name { get; set; }

    /// <summary>The SQL type name of the column (e.g., "nvarchar", "decimal").</summary>
    public string TypeName { get; set; }

    public Type PropertyType { get; set; }

    /// <summary>Optional precision for numeric types (e.g., decimal).</summary>
    public int? Precision { get; set; }

    /// <summary>Optional scale for numeric types (e.g., decimal).</summary>
    public int? Scale { get; set; }

    /// <summary>Indicates whether the column allows null values.</summary>
    public bool IsNullable { get; set; } = true;

    /// <summary>The default value assigned to the column, if any.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Optional collation setting for the column.</summary>
    public string? Collation { get; set; }

    /// <summary>Optional computed expression for virtual columns.</summary>
    public string? ComputedExpression { get; set; }

    /// <summary>Optional ordering value used to control column position in generated SQL.</summary>
    public int? Order { get; set; }

    /// <summary>Optional comment or annotation for the column.</summary>
    public string? Comment { get; set; }

    // 🔹 Constraints & Identity

    /// <summary>Indicates whether the column is an identity column.</summary>
    public bool IsIdentity { get; set; }

    /// <summary>Indicates whether the column is part of the primary key.</summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>Indicates whether the column has a unique constraint.</summary>
    public bool IsUnique { get; set; }

    /// <summary>The name of the unique constraint, if explicitly defined.</summary>
    public string? UniqueConstraintName { get; set; }

    /// <summary>A list of check constraints applied to this column.</summary>
    public List<CheckConstraintDefinition> CheckConstraints { get; set; } = new();

    // 🔹 Indexing

    /// <summary>A list of indexes that include this column.</summary>
    public List<IndexDefinition> Indexes { get; set; } = new();

    /// <summary>
    /// Indicates whether this column type is valid for indexing.
    /// </summary>
    public bool IsIndexable =>
        !TypeName.Contains("max", StringComparison.OrdinalIgnoreCase) &&
        !TypeName.Contains("text", StringComparison.OrdinalIgnoreCase) &&
        !TypeName.Contains("image", StringComparison.OrdinalIgnoreCase);

    // 🔹 Foreign Key & Navigation

    /// <summary>Indicates whether the column is a foreign key.</summary>
    public bool IsForeignKey { get; set; }

    /// <summary>The target entity name for the foreign key relationship.</summary>
    public string? ForeignKeyTarget { get; set; }

    /// <summary>Indicates whether this column represents a navigation property.</summary>
    public bool IsNavigationProperty { get; set; }

    // 🔹 Metadata & Control

    /// <summary>Indicates whether the column should be excluded from SQL generation.</summary>
    public bool IsIgnored { get; set; }

    /// <summary>Optional reason for ignoring the column.</summary>
    public string? IgnoreReason { get; set; }

    /// <summary>Optional description of the column, used for documentation or extended properties.</summary>
    public string? Description { get; set; }
    /// <summary>
    /// Indicates whether a computed column is persisted in the database.
    /// Only applies when <see cref="ComputedExpression"/> is not null or empty.
    /// </summary>
    public bool IsPersisted { get; set; }

}