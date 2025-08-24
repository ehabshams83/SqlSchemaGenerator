using Syn.Core.SqlSchemaGenerator.Migrations.AlterTable;

using System.Collections.Generic;

namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a database entity (typically a table) with its name, columns, and constraints.
    /// Used by the schema generator and migration engine to define and compare schema structures.
    /// </summary>
    public class EntityDefinition
    {
        /// <summary>
        /// Gets or sets the logical name of the entity (e.g., table name).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the schema name (e.g., "dbo", "public").
        /// </summary>
        public string Schema { get; set; } = "dbo";

        /// <summary>
        /// Gets or sets the list of columns defined in the entity.
        /// </summary>
        public List<ColumnDefinition> Columns { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of indexes defined on the entity.
        /// </summary>
        public List<IndexDefinition> Indexes { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of check constraints defined on the entity.
        /// </summary>
        public List<CheckConstraintDefinition> CheckConstraints { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of computed columns defined on the entity.
        /// </summary>
        public List<ComputedColumnDefinition> ComputedColumns { get; set; } = new();

        /// <summary>
        /// Gets or sets the optional description or comment for the entity.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether this entity should be ignored during generation.
        /// </summary>
        public bool IsIgnored { get; set; } = false;
    }
}