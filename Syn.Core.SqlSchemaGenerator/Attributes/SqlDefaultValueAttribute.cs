namespace Syn.Core.SqlSchemaGenerator.Attributes;

/// <summary>
/// Specifies a SQL expression to be used as the default value for a database column.
/// </summary>
/// <remarks>
/// This attribute is intended to define a default value at the database level.
/// The expression should be a valid SQL literal or function, such as <c>GETDATE()</c>, 
/// <c>NEWID()</c>, <c>0</c>, or <c>N'Unknown'</c>.
/// The default value is applied only when no explicit value is provided during an insert operation.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class SqlDefaultValueAttribute : Attribute
{
    /// <summary>
    /// Gets the SQL expression that will be used as the default value for the column.
    /// </summary>
    /// <value>
    /// A valid SQL expression, such as <c>GETDATE()</c>, <c>NEWID()</c>, <c>0</c>, or <c>N'Unknown'</c>.
    /// </value>
    public string Expression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlDefaultValueAttribute"/> class.
    /// </summary>
    /// <param name="expression">
    /// The SQL expression to use as the default value. Must be a valid SQL literal or function.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="expression"/> is null, empty, or consists only of white-space characters.
    /// </exception>
    public SqlDefaultValueAttribute(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("SQL default value expression cannot be null or empty.", nameof(expression));

        Expression = expression;
    }
}