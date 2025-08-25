using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Handles <see cref="ComputedAttribute"/> and marks the column as computed.
/// </summary>
public class ComputedAttributeHandler : ISchemaAttributeHandler
{
    /// <summary>
    /// Applies computed column metadata to the column model.
    /// Supports both [Computed] and [DatabaseGenerated(Computed)] attributes.
    /// </summary>
    public void Apply(PropertyInfo property, ColumnModel column)
    {
        // Check for [Computed]
        var computedAttr = property.GetCustomAttribute<ComputedAttribute>();
        if (computedAttr != null)
        {
            column.IsComputed = true;
            column.ComputedExpression = computedAttr.SqlExpression;
            column.ComputedSource = "[Computed]";

            if (!string.IsNullOrWhiteSpace(computedAttr.SqlExpression) &&
                !IsValidSqlExpression(computedAttr.SqlExpression))
            {
                throw new InvalidOperationException(
                    $"Invalid SQL expression on [Computed] for property '{property.Name}': {computedAttr.SqlExpression}");
            }

            return;
        }

        // Check for [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        var dbGenerated = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
        if (dbGenerated?.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed)
        {
            column.IsComputed = true;
            column.ComputedSource = "[DatabaseGenerated]";

            // No expression available, but still mark as computed
        }
    }

    /// <summary>
    /// Basic validation for SQL expressions used in computed columns.
    /// </summary>
    private static bool IsValidSqlExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        // Basic heuristics: must contain SQL-like syntax
        if (!expression.Contains("(") && !expression.Contains("+") && !expression.Contains(" "))
            return false;

        return true;
    }

}