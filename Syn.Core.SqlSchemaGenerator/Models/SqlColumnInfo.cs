namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents metadata about a SQL column derived from a .NET property.
    /// </summary>
    public class SqlColumnInfo
    {
        /// <summary>
        /// The name of the column in the SQL table.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The .NET type of the property.
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Indicates whether this column is nullable.
        /// </summary>
        public bool IsNullable { get; set; } = true;

        /// <summary>
        /// Indicates whether this column is the primary key.
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Indicates whether this column should have a UNIQUE constraint.
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Optional default value for the column.
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Indicates whether this column is computed using a SQL expression.
        /// </summary>
        public bool IsComputed { get; set; }

        /// <summary>
        /// The SQL expression used to compute the column value, if applicable.
        /// </summary>
        public string? ComputedExpression { get; set; }

        /// <summary>
        /// The name of the target table for a foreign key relationship, if applicable.
        /// </summary>
        public string? ForeignKeyTargetTable { get; set; }

        /// <summary>
        /// The name of the target column in the foreign table, typically the primary key.
        /// </summary>
        public string? ForeignKeyTargetColumn { get; set; }
        /// <summary>
        /// The SQL type name explicitly mapped from the .NET type or overridden via attribute.
        /// </summary>
        public string? TypeName { get; set; }

        /// <summary>
        /// Indicates whether the column is auto-incremented (IDENTITY).
        /// </summary>
        public bool IsIdentity { get; set; }
        /// <summary>
        /// Indicates whether this column is part of an index.
        /// </summary>
        public bool HasIndex { get; set; }

        /// <summary>
        /// Indicates whether the index is unique.
        /// </summary>
        public bool IsIndexUnique { get; set; }

        /// <summary>
        /// Optional index name if explicitly defined.
        /// </summary>
        public string? IndexName { get; set; }

        /// <summary>
        /// Optional documentation or comment for the column.
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>
        /// Optional collation for the column.
        /// </summary>
        public string? Collation { get; set; }

        /// <summary>
        /// Precision for numeric types (e.g. decimal).
        /// </summary>
        public int? Precision { get; set; }

        /// <summary>
        /// Scale for numeric types (e.g. decimal).
        /// </summary>
        public int? Scale { get; set; }

    }

}