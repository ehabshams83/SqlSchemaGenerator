using Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents a column within an entity, including metadata for SQL generation and schema analysis.
/// </summary>
public class ColumnModel
{
    /// <summary>Column name as defined in the database or model.</summary>
    public string Name { get; set; } = null!;

    /// <summary>CLR type of the property (e.g., typeof(string), typeof(int)).</summary>
    public Type PropertyType { get; set; } = typeof(string);

    /// <summary>Indicates whether the column should be ignored during generation.</summary>
    public bool IsIgnored { get; set; }

    /// <summary>Optional reason for ignoring the column.</summary>
    public string? IgnoreReason { get; set; }

    /// <summary>Indicates whether the column is computed.</summary>
    public bool IsComputed { get; set; }

    /// <summary>SQL expression used to compute the column.</summary>
    public string? ComputedExpression { get; set; }

    /// <summary>Source of the computed column (e.g., "Manual", "Attribute").</summary>
    public string? ComputedSource { get; set; }

    /// <summary>Indicates whether the computed column is persisted in the database.</summary>
    public bool IsPersisted { get; set; }

    /// <summary>Default value assigned to the column.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Collation applied to the column, if any.</summary>
    public string? Collation { get; set; }

    /// <summary>List of check constraints applied to the column.</summary>
    public List<CheckConstraintModel> CheckConstraints { get; set; } = new();

    /// <summary>List of indexes defined on the column.</summary>
    public List<IndexModel> Indexes { get; set; } = new();

    /// <summary>Indicates whether the column has a unique constraint.</summary>
    public bool IsUnique { get; set; }

    /// <summary>Name of the unique constraint, if applicable.</summary>
    public string? UniqueConstraintName { get; set; }

    /// <summary>Optional description or comment for the column.</summary>
    public string? Description { get; set; }

    /// <summary>Indicates whether the column allows null values.</summary>
    public bool IsNullable { get; set; }

    /// <summary>SQL type name (e.g., "nvarchar(100)", "int").</summary>
    public string? TypeName { get; set; }

    /// <summary>Optional name of the source entity this column belongs to.</summary>
    public string? SourceEntity { get; set; }

    /// <summary>Indicates whether the column is part of the primary key.</summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>Indicates whether the column is a foreign key.</summary>
    public bool IsForeignKey { get; set; }

    /// <summary>Target table and column for the foreign key (e.g., "Users(Id)").</summary>
    public string? ForeignKeyTarget { get; set; }

    /// <summary>Optional order of the column within the table definition.</summary>
    public int? Order { get; set; }
    /// <summary>
    /// Gets or sets the maximum length of the column, if applicable.
    /// </summary>
    public int MaxLength { get; set; }
}