namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a foreign key relationship between two tables in the schema.
    /// </summary>
    public class ForeignKeyDefinition
    {
        /// <summary>
        /// The name of the column in the current table that acts as the foreign key.
        /// </summary>
        public required string Column { get; set; }

        /// <summary>
        /// The name of the referenced table that this foreign key points to.
        /// </summary>
        public required string ReferencedTable { get; set; }

        /// <summary>
        /// The name of the referenced column in the target table. Defaults to "Id" if not specified.
        /// </summary>
        public string ReferencedColumn { get; set; } = "Id";

        /// <summary>
        /// The action to perform on delete (e.g., CASCADE, SET NULL, NO ACTION).
        /// </summary>
        public ReferentialAction OnDelete { get; set; } = ReferentialAction.NoAction;

        /// <summary>
        /// The action to perform on update (e.g., CASCADE, SET NULL, NO ACTION).
        /// </summary>
        public ReferentialAction OnUpdate { get; set; } = ReferentialAction.NoAction;

        /// <summary>
        /// Optional constraint name for the foreign key. If not set, it will be auto-generated.
        /// </summary>
        public string? ConstraintName { get; set; }
    }

    
}