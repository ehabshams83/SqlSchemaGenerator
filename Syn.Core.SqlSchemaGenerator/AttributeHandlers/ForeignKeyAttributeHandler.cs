using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers
{
    /// <summary>
    /// Marks column as a foreign key and stores reference metadata from <see cref="ForeignKeyAttribute"/>.
    /// </summary>
    public class ForeignKeyAttributeHandler : ISchemaAttributeHandler
    {
        public void Apply(PropertyInfo property, ColumnModel column)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(column);

            var attr = property.GetCustomAttribute<ForeignKeyAttribute>(inherit: true);
            if (attr is null)
                return;

            column.IsForeignKey = true;
            column.ForeignKeyTable = attr.TargetTable;
            column.ForeignKeyColumn = attr.TargetColumn;
        }
    }
}