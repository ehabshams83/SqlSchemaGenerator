using System.Collections.Generic;

namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a constraint definition applied to an entity or column.
    /// Supports check constraints, foreign keys, and enforcement metadata.
    /// </summary>
    public class ConstraintModel
    {
        /// <summary>
        /// The name of the constraint.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of constraint (e.g., Check, ForeignKey, Unique).
        /// </summary>
        public ConstraintType Type { get; set; }

        /// <summary>
        /// The SQL expression used to define the constraint logic.
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// Optional description of the constraint, used for documentation or extended properties.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The list of column names affected by the constraint.
        /// </summary>
        public List<string> Columns { get; set; } = new();

        /// <summary>
        /// The name of the target table in case of a foreign key constraint.
        /// </summary>
        public string? ForeignKeyTargetTable { get; set; }

        /// <summary>
        /// The list of target columns in the foreign key relationship.
        /// </summary>
        public List<string>? ForeignKeyTargetColumns { get; set; }

        /// <summary>
        /// Indicates whether the constraint is enforced by the database engine.
        /// </summary>
        public bool IsEnforced { get; set; }

        /// <summary>
        /// Indicates whether the constraint is trusted by the query optimizer.
        /// </summary>
        public bool IsTrusted { get; set; }
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