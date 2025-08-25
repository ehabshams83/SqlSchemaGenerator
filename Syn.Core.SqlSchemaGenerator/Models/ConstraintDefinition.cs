namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a primary key or foreign key constraint definition.
    /// </summary>
    public class ConstraintDefinition
    {
        /// <summary>
        /// Constraint name in the database.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Constraint type (PRIMARY KEY, FOREIGN KEY, UNIQUE, etc.).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Ordered list of columns included in the constraint.
        /// </summary>
        public List<string> Columns { get; set; } = new();
        public string? ReferencedTable { get; set; }
        public List<string> ReferencedColumns { get; set; } = new();
    }
}