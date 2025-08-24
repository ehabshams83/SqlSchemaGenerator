namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a check constraint defined on an entity.
    /// </summary>
    public class CheckConstraintDefinition
    {
        /// <summary>
        /// Gets or sets the logical name of the check constraint.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SQL expression that defines the constraint logic.
        /// </summary>
        public string Expression { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional description or comment for the constraint.
        /// </summary>
        public string? Description { get; set; }
    }
}