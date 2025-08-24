using System.Collections.Generic;

namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a database constraint (e.g., CHECK, UNIQUE, FOREIGN KEY).
    /// </summary>
    public class ConstraintModel
    {
        /// <summary>
        /// The name of the constraint in the database.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of constraint (e.g., Check, Unique, ForeignKey).
        /// </summary>
        public ConstraintType Type { get; set; }

        /// <summary>
        /// The SQL expression used for the constraint (e.g., CHECK expression).
        /// </summary>
        public string Expression { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// The list of columns involved in the constraint (if applicable).
        /// </summary>
        public List<string> Columns { get; set; } = new();

        /// <summary>
        /// Optional: Target table for foreign key constraints.
        /// </summary>
        public string? ForeignKeyTargetTable { get; set; }

        /// <summary>
        /// Optional: Target column(s) for foreign key constraints.
        /// </summary>
        public List<string>? ForeignKeyTargetColumns { get; set; }

        /// <summary>
        /// Optional: Whether the constraint is enforced.
        /// </summary>
        public bool IsEnforced { get; set; } = true;

        /// <summary>
        /// Optional: Whether the constraint is initially trusted.
        /// </summary>
        public bool IsTrusted { get; set; } = true;
    }

    /// <summary>
    /// Enum representing supported constraint types.
    /// </summary>
    public enum ConstraintType
    {
        Check,
        Unique,
        PrimaryKey,
        ForeignKey
    }
}