using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="UniqueAttribute"/> and marks the column as unique.
/// </summary>
//public class UniqueAttributeHandler : ISchemaAttributeHandler
//{
//    /// <summary>
//    /// Applies unique constraint metadata to the column model.
//    /// </summary>
//    public void Apply(PropertyInfo property, ColumnModel column)
//    {
//        var attr = property.GetCustomAttribute<UniqueAttribute>();
//        if (attr != null)
//        {
//            column.IsUnique = true;
//            column.UniqueConstraintName = attr.ConstraintName;
//        }
//    }
//}