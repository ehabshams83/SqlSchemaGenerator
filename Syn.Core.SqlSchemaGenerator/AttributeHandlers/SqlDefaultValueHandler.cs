using Syn.Core.SqlSchemaGenerator.Attributes;

using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Column handler that reads <see cref="SqlDefaultValueAttribute"/> from a property
/// and applies its SQL expression as the column's default value.
/// </summary>
public class SqlDefaultValueHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies the SQL default value from <see cref="SqlDefaultValueAttribute"/> to the given column model.
    /// </summary>
    /// <param name="prop">The property being processed.</param>
    /// <param name="column">The column model to update.</param>
    public void Apply(PropertyInfo prop, ColumnModel column)
    {
        if (prop == null) throw new ArgumentNullException(nameof(prop));
        if (column == null) throw new ArgumentNullException(nameof(column));

        var attr = prop.GetCustomAttribute<SqlDefaultValueAttribute>();
        if (attr != null && !string.IsNullOrWhiteSpace(attr.Expression))
        {
            // Store the SQL expression exactly as provided
            column.DefaultValue = attr.Expression;

            Console.WriteLine(
                $"[TRACE:DefaultValue] {prop.DeclaringType?.Name}.{prop.Name} → SQL Default Expression = {attr.Expression}"
            );
        }
    }
}