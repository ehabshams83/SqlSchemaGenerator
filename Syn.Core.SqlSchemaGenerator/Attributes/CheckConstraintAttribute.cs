namespace Syn.Core.SqlSchemaGenerator.Attributes;

/// <summary>
/// Adds a CHECK constraint to the column.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CheckConstraintAttribute : Attribute
{
    /// <summary>
    /// The SQL expression to validate.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Optional name of the constraint.
    /// </summary>
    public string? Name { get; set; }

    public CheckConstraintAttribute(string expression)
    {
        Expression = expression;
    }
}