using Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Represents a database column within an entity model.
/// Includes metadata for SQL generation, constraints, indexing, and documentation.
/// </summary>
public class ColumnModel
{
    /// <summary>
    /// The name of the column as defined in the source code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The CLR type of the property.
    /// </summary>
    public Type PropertyType { get; set; }

    /// <summary>
    /// Indicates whether this column should be excluded from SQL generation.
    /// </summary>
    public bool IsIgnored { get; set; }

    /// <summary>
    /// Optional reason for ignoring the column, useful for diagnostics or documentation.
    /// </summary>
    public string? IgnoreReason { get; set; }

    /// <summary>
    /// Indicates whether the column is computed (i.e., derived from an expression).
    /// </summary>
    public bool IsComputed { get; set; }

    /// <summary>
    /// The SQL expression used to compute the column value.
    /// </summary>
    public string? ComputedExpression { get; set; }

    /// <summary>
    /// The source property name from which the computed column was derived.
    /// </summary>
    public string? ComputedSource { get; set; }

    /// <summary>
    /// Indicates whether the computed column is persisted in the database.
    /// </summary>
    public bool IsPersisted { get; set; }
    /// <summary>Indicates whether the column is an identity column.</summary>
    public bool IsIdentity { get; set; }

    /// <summary>
    /// The default value assigned to the column, if any.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Optional collation setting for the column.
    /// </summary>
    public string? Collation { get; set; }

    /// <summary>
    /// A list of check constraints applied to this column.
    /// </summary>
    public List<CheckConstraintModel> CheckConstraints { get; set; } = new();

    /// <summary>
    /// A list of indexes that include this column.
    /// </summary>
    public List<IndexModel> Indexes { get; set; } = new();

    /// <summary>
    /// Indicates whether the column has a unique constraint.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// The name of the unique constraint, if explicitly defined.
    /// </summary>
    public string? UniqueConstraintName { get; set; }

    /// <summary>
    /// Optional description of the column, used for documentation or extended properties.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether the column allows null values.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Optional SQL type name override (e.g., "nvarchar(100)").
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// The name of the source entity from which this column was derived.
    /// </summary>
    public string? SourceEntity { get; set; }

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

    /// <summary>
    /// The maximum allowed length of the column (used for strings or binary types).
    /// </summary>
    public int? MaxLength { get; set; }
    /// <summary>
    /// Numeric precision for decimal or numeric data types.
    /// Null if not applicable.
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    /// Numeric scale for decimal or numeric data types.
    /// Null if not applicable.
    /// </summary>
    public int? Scale { get; set; }
    public string? ForeignKeyTable { get; set; }
    public string? ForeignKeyColumn { get; set; }

    public string Comment { get; set; }  
}