namespace Syn.Core.SqlSchemaGenerator.Attributes
{
    /// <summary>
    /// Marks a property as a computed column with a SQL expression.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ComputedAttribute : Attribute
    {
        /// <summary>
        /// SQL expression used to compute the column value.
        /// </summary>
        public string SqlExpression { get; }

        /// <summary>
        /// Indicates whether the computed column is persisted in the database.
        /// </summary>
        public bool IsPersisted { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComputedAttribute"/> class.
        /// </summary>
        /// <param name="sqlExpression">SQL expression used to compute the column value.</param>
        /// <param name="isPersisted">Whether the column is persisted in the database.</param>
        public ComputedAttribute(string sqlExpression, bool isPersisted = false)
        {
            SqlExpression = sqlExpression;
            IsPersisted = isPersisted;
        }
    }
}