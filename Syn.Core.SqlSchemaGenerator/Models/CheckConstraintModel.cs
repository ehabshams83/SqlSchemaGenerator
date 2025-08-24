namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a check constraint applied to a column or entity.
    /// Defines a SQL expression and optional name and description.
    /// </summary>
    public class CheckConstraintModel
    {
        /// <summary>
        /// The SQL expression that defines the constraint logic.
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// Optional name of the constraint. If null, a default name may be generated.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Optional description of the constraint, used for documentation or extended properties.
        /// </summary>
        public string? Description { get; set; }
    }
}
