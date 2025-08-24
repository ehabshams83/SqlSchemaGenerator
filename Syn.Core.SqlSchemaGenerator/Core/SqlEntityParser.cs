using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Core
{
    /// <summary>
    /// Parses a .NET entity type into a list of SQL column definitions.
    /// </summary>
    public class SqlEntityParser
    {
        /// <summary>
        /// Extracts SQL column metadata from the given .NET type.
        /// </summary>
        public List<SqlColumnInfo> Parse(Type type)
        {
            var columns = new List<SqlColumnInfo>();

            foreach (var prop in type.GetProperties())
            {
                var col = new SqlColumnInfo
                {
                    Name = prop.Name,
                    Type = prop.PropertyType,
                    IsPrimaryKey = prop.Name == "Id" || prop.Name == $"{type.Name}Id",
                    IsUnique = prop.GetCustomAttribute<UniqueAttribute>() != null,
                    IsComputed = prop.GetCustomAttribute<ComputedAttribute>() != null,
                    ComputedExpression = prop.GetCustomAttribute<ComputedAttribute>()?.SqlExpression
                };

                if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string))
                {
                    col.ForeignKeyTargetTable = prop.PropertyType.Name;
                    col.ForeignKeyTargetColumn = "Id";
                }

                columns.Add(col);
            }

            return columns;
        }
    }
}