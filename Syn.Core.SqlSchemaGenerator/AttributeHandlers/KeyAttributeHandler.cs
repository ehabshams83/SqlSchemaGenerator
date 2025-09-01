using System.ComponentModel.DataAnnotations;
using System.Reflection;

using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Schema attribute handler that maps the [Key] attribute
/// to Primary Key metadata in the ColumnModel.
/// </summary>
public class KeyAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies the [Key] attribute effect to the column model.
    /// Marks the column as a primary key and ensures it is non-nullable.
    /// </summary>
    /// <param name="prop">Property info being scanned.</param>
    /// <param name="column">Column model to update.</param>
    public void Apply(PropertyInfo prop, ColumnModel column)
    {
        if (prop == null || column == null)
            return;

        var keyAttr = prop.GetCustomAttribute<KeyAttribute>();
        if (keyAttr == null)
            return;

        column.IsPrimaryKey = true;
        column.IsNullable = false;

        // إضافة تعليق توضيحي في الموديل
        column.Comment ??= "[Key] attribute detected - marked as Primary Key";

        // (اختياري) لو مفيش اسم مفتاح أساسي محدد في مكان آخر
        if (string.IsNullOrWhiteSpace(column.UniqueConstraintName))
        {
            column.UniqueConstraintName = $"PK_{prop.DeclaringType?.Name}";
        }
    }
}