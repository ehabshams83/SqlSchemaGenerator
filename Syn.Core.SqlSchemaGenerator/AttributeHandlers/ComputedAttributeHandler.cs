using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Attributes;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="ComputedAttribute"/> and marks the column as computed.
/// </summary>
public class ComputedAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies computed column metadata to the column model.
    /// </summary>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        var attr = property.GetCustomAttribute<ComputedAttribute>();
        if (attr != null)
        {
            column.IsComputed = true;
            column.ComputedExpression = attr.SqlExpression;
            column.ComputedSource = "[Computed]";
        }
    }
}