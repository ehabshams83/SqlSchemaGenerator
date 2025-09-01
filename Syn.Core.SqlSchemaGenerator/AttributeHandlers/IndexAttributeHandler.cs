using System.Reflection;

using Microsoft.EntityFrameworkCore;

using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="IndexAttribute"/> and adds index metadata to the column.
/// Ensures that only indexable column types are processed.
/// </summary>
public class IndexAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies index attributes found on the property to the column model.
    /// Skips index creation if the column type is not indexable (e.g., nvarchar(max), text, image).
    /// </summary>
    /// <param name="property">The CLR property decorated with [Index].</param>
    /// <param name="column">The column model to update.</param>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        if (property == null || column == null)
            return;

        var attributes = property.GetCustomAttributes<IndexAttribute>();

        foreach (var attr in attributes)
        {
            // ✅ فحص صلاحية النوع للفهرسة
            if (!IsIndexable(column.TypeName))
            {
                column.Comment ??= "Index skipped due to unsupported SQL type.";
                continue;
            }

            var index = new IndexModel
            {
                Name = string.IsNullOrWhiteSpace(attr.Name)
                    ? $"IX_{column.Name}"
                    : attr.Name,
                IsUnique = attr.IsUnique,
                IncludeColumns = attr.PropertyNames?.ToList() ?? new List<string>()
            };

            column.Indexes.Add(index);
        }
    }

    /// <summary>
    /// Determines whether the given SQL type is valid for indexing.
    /// </summary>
    private bool IsIndexable(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        var lowered = typeName.ToLowerInvariant();
        return !(lowered.Contains("max") || lowered.Contains("text") || lowered.Contains("image"));
    }
}