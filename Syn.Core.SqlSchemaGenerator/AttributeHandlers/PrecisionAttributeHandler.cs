using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers
{
    /// <summary>
    /// Sets numeric precision and scale from <see cref="PrecisionAttribute"/>.
    /// </summary>
    public class PrecisionAttributeHandler : ISchemaAttributeHandler
    {
        public void Apply(PropertyInfo property, ColumnModel column)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(column);

            var attr = property.GetCustomAttribute<PrecisionAttribute>(inherit: true);
            if (attr is null)
                return;

            column.Precision = attr.Precision;
            column.Scale = attr.Scale;
        }
    }
}