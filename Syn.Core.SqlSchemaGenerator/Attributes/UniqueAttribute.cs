namespace Syn.Core.SqlSchemaGenerator.Attributes
{
    /// <summary>
    /// Marks the column as having a UNIQUE constraint.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UniqueAttribute : Attribute
    {
        /// <summary>
        /// Optional name of the unique constraint.
        /// </summary>
        public string? ConstraintName { get; set; }
    }
}
