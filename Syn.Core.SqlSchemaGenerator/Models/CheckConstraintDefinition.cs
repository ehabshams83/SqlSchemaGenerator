namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a physical check constraint used in SQL generation.
    /// Includes name, expression, and optional description.
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
    }
}