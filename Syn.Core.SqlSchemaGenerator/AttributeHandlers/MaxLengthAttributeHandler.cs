using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers
{
    public class MaxLengthAttributeHandler : ISchemaAttributeHandler
    {
        public void Apply(PropertyInfo property, ColumnModel column)
        {
            var attr = property.GetCustomAttribute<MaxLengthAttribute>();
            if (attr != null)
            {
                column.MaxLength = attr.Length;
            }
        }
    }
}
