using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Attributes;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="CheckConstraintAttribute"/> and adds check constraint metadata.
/// </summary>
public class CheckConstraintAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies check constraint metadata to the column model.
    /// </summary>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        var attr = property.GetCustomAttribute<CheckConstraintAttribute>();
        if (attr != null)
        {
            column.CheckConstraints.Add(new CheckConstraintModel
            {
                Expression = attr.Expression,
                Name = attr.Name
            });
        }
    }
}