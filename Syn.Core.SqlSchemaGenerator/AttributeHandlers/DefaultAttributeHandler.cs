using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Attributes;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="DefaultValueAttribute"/> and sets the default value for the column.
/// </summary>
public class DefaultValueAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies default value metadata to the column model.
    /// </summary>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        var attr = property.GetCustomAttribute<DefaultValueAttribute>();
        if (attr != null)
        {
            column.DefaultValue = attr.Value?.ToString();
        }
    }
}