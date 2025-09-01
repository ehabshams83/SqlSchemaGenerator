using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles EF Core's <see cref="ForeignKeyAttribute"/> and maps it to foreign key metadata
/// in the <see cref="ColumnModel"/>.
/// </summary>
public class ForeignKeyAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies EF Core's [ForeignKey] attribute to the column model.
    /// Marks the column as a foreign key and stores the target navigation/column name.
    /// </summary>
    /// <param name="property">The CLR property decorated with [ForeignKey].</param>
    /// <param name="column">The column model to update.</param>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        if (property == null || column == null)
            return;

        var attr = property.GetCustomAttribute<ForeignKeyAttribute>();
        if (attr == null)
            return;

        column.IsForeignKey = true;
        column.ForeignKeyTarget = attr.Name;

        // إضافة تعليق للتوثيق في التقارير
        column.Comment ??= $"[ForeignKey] → Target: {attr.Name}";
    }
}