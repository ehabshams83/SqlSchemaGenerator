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
        /// Indicates whether this column is the primary key.
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Indicates whether this column should have a UNIQUE constraint.
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Indicates whether this column is computed using a SQL expression.
        /// </summary>
        public bool IsComputed { get; set; }

        /// <summary>
        /// The SQL expression used to compute the column value, if applicable.
        /// </summary>
        public string ComputedExpression { get; set; }

        /// <summary>
        /// The name of the target table for a foreign key relationship, if applicable.
        /// </summary>
        public string ForeignKeyTargetTable { get; set; }

        /// <summary>
        /// The name of the target column in the foreign table, typically the primary key.
        /// </summary>
        public string ForeignKeyTargetColumn { get; set; }
    }
}