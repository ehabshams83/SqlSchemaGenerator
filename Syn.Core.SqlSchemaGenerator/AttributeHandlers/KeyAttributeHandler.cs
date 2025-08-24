using Syn.Core.SqlSchemaGenerator.AttributeHandlers;

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;
/// <summary>
/// Schema attribute handler that maps [Key] attribute
/// to Primary Key metadata in the ColumnModel.
/// </summary>
public class KeyAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies the [Key] attribute effect to the column model.
    /// </summary>
    /// <param name="prop">Property info being scanned.</param>
    /// <param name="column">Column model to update.</param>
    public void Apply(PropertyInfo prop, ColumnModel column)
    {
        var keyAttr = prop.GetCustomAttribute<KeyAttribute>();
        if (keyAttr != null)
        {
            column.IsPrimaryKey = true;
            column.IsNullable = false;
        }
    }
}