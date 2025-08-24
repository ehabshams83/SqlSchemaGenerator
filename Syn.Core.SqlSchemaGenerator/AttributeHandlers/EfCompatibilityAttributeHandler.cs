using Syn.Core.SqlSchemaGenerator.Models;

using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Provides compatibility with common Entity Framework Core attributes.
/// Maps EF-style annotations to internal schema metadata.
/// </summary>
public class EfCompatibilityAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies EF Core-compatible attributes to the column model.
    /// Supports [NotMapped] and [DatabaseGenerated(DatabaseGeneratedOption.Computed)].
    /// </summary>
    /// <param name="property">The property being inspected.</param>
    /// <param name="column">The column model to update.</param>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        // Handle [NotMapped] → IgnoreColumn
        if (property.GetCustomAttribute<NotMappedAttribute>() is not null)
        {
            column.IsIgnored = true;
            column.IgnoreReason = "Marked with [NotMapped] (EF Core)";
        }

        // Handle [DatabaseGenerated(DatabaseGeneratedOption.Computed)] → Computed
        var dbGen = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
        if (dbGen?.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed)
        {
            column.IsComputed = true;
            column.ComputedExpression ??= null; // Leave null unless overridden by [Computed]
            column.ComputedSource = "EF Core [DatabaseGenerated]";
        }
    }
}