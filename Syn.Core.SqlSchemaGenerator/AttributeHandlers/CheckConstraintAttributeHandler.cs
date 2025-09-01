using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Attributes;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="CheckConstraintAttribute"/> and adds check constraint metadata.
/// Ensures that the constraint expression is valid before adding it.
/// </summary>
public class CheckConstraintAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies check constraint metadata to the column model.
    /// Skips invalid or empty constraint expressions.
    /// </summary>
    /// <param name="property">The CLR property decorated with [CheckConstraintAttribute].</param>
    /// <param name="column">The column model to update.</param>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        if (property == null || column == null)
            return;

        var attr = property.GetCustomAttribute<CheckConstraintAttribute>();
        if (attr == null)
            return;

        // ✅ فحص صلاحية التعبير
        if (string.IsNullOrWhiteSpace(attr.Expression))
        {
            column.Comment ??= "Check constraint skipped due to empty SQL expression.";
            return;
        }

        // ✅ توليد اسم افتراضي لو مش متحدد
        var constraintName = string.IsNullOrWhiteSpace(attr.Name)
            ? $"CK_{property.DeclaringType?.Name}_{column.Name}"
            : attr.Name;

        column.CheckConstraints.Add(new CheckConstraintModel
        {
            Expression = attr.Expression,
            Name = constraintName
        });
    }
}