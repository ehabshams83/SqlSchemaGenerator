namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a physical check constraint used in SQL generation.
    /// Includes name, expression, optional description, and referenced columns for indexing.
    /// </summary>
    public class CheckConstraintDefinition
    {
        /// <summary>
        /// The name of the check constraint.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The SQL expression that defines the constraint logic.
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// Optional description of the constraint, used for documentation or extended properties.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// List of column names referenced in the expression.
        /// Used to generate supporting indexes or statistics.
        /// </summary>
        public List<string> ReferencedColumns { get; set; } = new();
    }
}