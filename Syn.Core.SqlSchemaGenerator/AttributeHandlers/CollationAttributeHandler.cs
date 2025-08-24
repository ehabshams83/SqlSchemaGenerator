using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Attributes;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="CollationAttribute"/> and sets collation metadata for the column.
/// </summary>
public class CollationAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies collation metadata to the column model.
    /// </summary>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        var attr = property.GetCustomAttribute<CollationAttribute>();
        if (attr != null)
        {
            column.Collation = attr.Collation;
        }
    }
}