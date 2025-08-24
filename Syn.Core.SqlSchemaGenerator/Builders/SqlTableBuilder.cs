using System.Text;

using Syn.Core.SqlSchemaGenerator.Core;
using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Generates SQL CREATE TABLE scripts based on entity metadata.
    /// </summary>
    public class SqlTableBuilder
    {
        /// <summary>
        /// Builds a CREATE TABLE SQL script for the specified entity type.
        /// </summary>
        public string Build(Type entityType)
        {
            var parser = new SqlEntityParser();
            var columns = parser.Parse(entityType);
            var sb = new StringBuilder();

            sb.AppendLine($"CREATE TABLE [{entityType.Name}] (");

            foreach (var col in columns)
            {
                if (col.IsComputed)
                {
                    sb.AppendLine($"  [{col.Name}] AS ({col.ComputedExpression}),");
                    continue;
                }

                var line = $"  [{col.Name}] {SqlTypeMapper.Map(col.Type)}";

                if (col.IsPrimaryKey) line += " PRIMARY KEY";
                if (col.IsUnique) line += " UNIQUE";

                sb.AppendLine(line + ",");
            }

            foreach (var col in columns.Where(c => c.ForeignKeyTargetTable != null))
            {
                sb.AppendLine($"  FOREIGN KEY ([{col.Name}]) REFERENCES [{col.ForeignKeyTargetTable}]([{col.ForeignKeyTargetColumn}]),");
            }

            sb.Remove(sb.Length - 3, 1); // remove last comma
            sb.AppendLine(");");

            return sb.ToString();
        }
    }
}