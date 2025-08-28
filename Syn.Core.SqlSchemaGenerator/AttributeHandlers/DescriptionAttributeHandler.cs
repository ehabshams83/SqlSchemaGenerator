using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;
using System.ComponentModel;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="DescriptionAttribute"/> and sets description metadata.
/// </summary>
public class DescriptionAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies description metadata to the column model.
    /// </summary>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        var attr = property.GetCustomAttribute<DescriptionAttribute>();
        if (attr != null)
        {
            column.Description = attr.Description;
        }
    }
}