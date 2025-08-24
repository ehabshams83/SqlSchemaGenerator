using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Converters
{
    /// <summary>
    /// Converts constraint models into SQL-ready definitions.
    /// </summary>
    public static class ConstraintConverter
    {
        /// <summary>
        /// Converts a CheckConstraintModel into a CheckConstraintDefinition.
        /// </summary>
        public static CheckConstraintDefinition FromCheckDefinition(this CheckConstraintModel model)
        {
            return new CheckConstraintDefinition
            {
                Name = model.Name ?? $"CK_{Guid.NewGuid():N}",
                Expression = model.Expression,
                Description = model.Description
            };
        }

        /// <summary>
        /// Extracts column names from a SQL expression (placeholder logic).
        /// </summary>
        public static List<string> ExtractColumnsFromExpression(string expression)
        {
            // TODO: Implement proper SQL parsing
            return new List<string>();
        }
    }
}