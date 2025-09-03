
using System.Collections.Generic;

namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a finalized entity definition used for SQL generation.
    /// Includes columns, indexes, constraints, computed columns, and optional metadata.
    /// </summary>
    public class EntityDefinition
    {
        /// <summary>
        /// The name of the entity (e.g., table or view name).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The schema name under which the entity resides.
        /// </summary>
        public string Schema { get; set; }

        /// <summary>
        /// The list of physical columns defined in the entity.
        /// </summary>
        public List<ColumnDefinition> Columns { get; set; } = new();

        /// <summary>
        /// The list of indexes defined for the entity.
        /// </summary>
        public List<IndexDefinition> Indexes { get; set; } = new();
        public List<ConstraintDefinition> Constraints { get; set; } = [];

        /// <summary>
        /// The list of check constraints applied to the entity.
        /// </summary>
        public List<CheckConstraintDefinition> CheckConstraints { get; set; } = new();

        /// <summary>
        /// The list of computed columns defined in the entity.
        /// </summary>
        public List<ComputedColumnDefinition> ComputedColumns { get; set; } = new();

        /// <summary>
        /// Optional description of the entity, used for documentation or extended properties.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Indicates whether the entity should be excluded from SQL generation.
        /// </summary>
        public bool IsIgnored { get; set; }
        /// <summary>
        /// The primary key definition for the entity, if any.
        /// </summary>
        public PrimaryKeyDefinition? PrimaryKey { get; set; }

        /// <summary>
        /// The list of unique constraints defined for the entity.
        /// </summary>
        public List<UniqueConstraintDefinition> UniqueConstraints { get; set; } = new();

        public List<ForeignKeyDefinition> ForeignKeys { get; set; } = [];
        public Type ClrType { get; set; }
        public List<RelationshipDefinition> Relationships { get; set; } = [];

    }
}