namespace Syn.Core.SqlSchemaGenerator.Attributes;

/// <summary>
/// Specifies a default value for the column.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DefaultValueAttribute : Attribute
{
    /// <summary>
    /// The default value to assign in SQL.
    /// </summary>
    public object? Value { get; }

    public DefaultValueAttribute(object value)
    {
        Value = value;
    }
}