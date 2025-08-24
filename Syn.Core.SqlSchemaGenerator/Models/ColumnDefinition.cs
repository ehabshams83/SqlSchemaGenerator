namespace Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents a standard column definition within an entity.
/// Used for schema declaration and migration comparison.
/// </summary>
public class ColumnDefinition
{
    /// <summary>Name of the column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SQL type name (e.g., "nvarchar(100)", "int").</summary>
    public string TypeName { get; set; } = "nvarchar(max)";

    /// <summary>Indicates whether the column allows null values.</summary>
    public bool IsNullable { get; set; } = true;

    /// <summary>Default value assigned to the column.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Collation applied to the column, if any.</summary>
    public string? Collation { get; set; }

    /// <summary>Optional description or comment for the column.</summary>
    public string? Description { get; set; }

    /// <summary>Indicates whether the column has a unique constraint.</summary>
    public bool IsUnique { get; set; }

    /// <summary>Name of the unique constraint, if applicable.</summary>
    public string? UniqueConstraintName { get; set; }

    /// <summary>List of index definitions applied to the column.</summary>
    public List<IndexDefinition> Indexes { get; set; } = new();

    /// <summary>List of check constraints applied to the column.</summary>
    public List<CheckConstraintDefinition> CheckConstraints { get; set; } = new();

    /// <summary>Indicates whether the column should be ignored during generation.</summary>
    public bool IsIgnored { get; set; }

    /// <summary>Optional reason for ignoring the column.</summary>
    public string? IgnoreReason { get; set; }

    /// <summary>Indicates whether the column is part of the primary key.</summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>Indicates whether the column is a foreign key.</summary>
    public bool IsForeignKey { get; set; }

    /// <summary>Target table and column for the foreign key (e.g., "Users(Id)").</summary>
    public string? ForeignKeyTarget { get; set; }

    /// <summary>Optional order of the column within the table definition.</summary>
    public int? Order { get; set; }
}