using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers
{
    public class RequiredAttributeHandler : ISchemaAttributeHandler
    {
        public void Apply(PropertyInfo property, ColumnModel column)
        {
            var attr = property.GetCustomAttribute<RequiredAttribute>();
            if (attr != null)
            {
                column.IsNullable = false;
            }
        }
    }
}
