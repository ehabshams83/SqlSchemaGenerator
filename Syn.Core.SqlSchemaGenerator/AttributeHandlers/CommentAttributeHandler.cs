using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers
{
    /// <summary>
    /// Applies <see cref="CommentAttribute"/> to the column description.
    /// </summary>
    public class CommentAttributeHandler : ISchemaAttributeHandler
    {
        public void Apply(PropertyInfo property, ColumnModel column)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            if (column == null) throw new ArgumentNullException(nameof(column));

            var attr = property.GetCustomAttribute<CommentAttribute>();
            if (attr != null)
                column.Description = attr.Text;
        }
    }
}