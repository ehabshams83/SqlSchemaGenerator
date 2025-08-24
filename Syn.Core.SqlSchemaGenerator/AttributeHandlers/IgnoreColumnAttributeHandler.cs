using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Attributes;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="IgnoreColumnAttribute"/> and marks the column to be ignored.
/// </summary>
public class IgnoreColumnAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Marks the column as ignored in the schema model.
    /// </summary>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        if (property.GetCustomAttribute<IgnoreColumnAttribute>() != null)
        {
            column.IsIgnored = true;
            column.IgnoreReason = "Marked with [IgnoreColumn]";
        }
    }
}