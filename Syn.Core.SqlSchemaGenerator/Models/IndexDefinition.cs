namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Represents a database index defined on one or more columns of an entity.
    /// </summary>
    public class IndexDefinition
    {
        /// <summary>
        /// Gets or sets the logical name of the index.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of column names included in the index.
        /// </summary>
        public List<string> Columns { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the index is unique.
        /// </summary>
        public bool IsUnique { get; set; } = false;

        /// <summary>
        /// Gets or sets an optional filter expression for partial indexes.
        /// </summary>
        public string? FilterExpression { get; set; }

        /// <summary>
        /// Gets or sets an optional description or comment for the index.
        /// </summary>
        public string? Description { get; set; }
    }
}