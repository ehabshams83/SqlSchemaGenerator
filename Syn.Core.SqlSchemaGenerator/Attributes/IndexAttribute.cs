namespace Syn.Core.SqlSchemaGenerator.Attributes
{
    /// <summary>
    /// Specifies an index on the column. Can be named and marked as unique.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class IndexAttribute : Attribute
    {
        /// <summary>
        /// Optional name of the index.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Indicates whether the index is unique.
        /// </summary>
        public bool IsUnique { get; set; } = false;

        /// <summary>
        /// Optional list of included columns for covering index.
        /// </summary>
        public string[]? Columns { get; set; }
    }
}