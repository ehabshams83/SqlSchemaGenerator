using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Converters
{
    /// <summary>
    /// Converts ColumnModel objects into ComputedColumnDefinition structures.
    /// </summary>
    public static class ComputedColumnConverter
    {
        /// <summary>
        /// Converts a ColumnModel into a ComputedColumnDefinition.
        /// </summary>
        public static ComputedColumnDefinition ToComputedColumnDefinition(this ColumnModel model)
        {
            return new ComputedColumnDefinition
            {
                Name = model.Name,
                DataType = model.TypeName ?? MapClrTypeToSql(model.PropertyType),
                Expression = model.ComputedExpression ?? "",
                IsPersisted = model.IsPersisted,
                Description = model.Description,
                Source = model.ComputedSource,
                IsIgnored = model.IsIgnored,
                IgnoreReason = model.IgnoreReason,
                Order = model.Order
            };
        }

        private static string MapClrTypeToSql(Type type)
        {
            if (type == typeof(string)) return "nvarchar(max)";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bit";
            if (type == typeof(DateTime)) return "datetime";
            if (type == typeof(decimal)) return "decimal(18,2)";
            return "nvarchar(max)";
        }
    }
}