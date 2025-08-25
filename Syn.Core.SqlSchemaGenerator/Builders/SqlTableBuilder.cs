using Syn.Core.SqlSchemaGenerator.Core;
using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Generates SQL CREATE TABLE scripts based on entity metadata.
    /// </summary>
    public partial class SqlTableBuilder
    {
        /// <summary>
        /// Builds a CREATE TABLE SQL script for the specified entity type.
        /// </summary>
        public string Build(Type entityType)
        {
            var parser = new SqlEntityParser();
            var columns = parser.Parse(entityType);
            var (tableName, schema) = entityType.ParseTableInfo(); // ميثود منفصلة لاستخراج اسم الجدول والـ schema

            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [{schema}].[{tableName}] (");

            // الأعمدة العادية
            foreach (var col in columns.Where(c => !c.IsComputed))
            {
                var line = $"  [{col.Name}] {col.TypeName}";

                if (!col.IsNullable) line += " NOT NULL";
                if (col.IsIdentity) line += " IDENTITY(1,1)";
                if (!string.IsNullOrWhiteSpace(col.DefaultValue)) line += $" DEFAULT {col.DefaultValue}";
                if (col.Collation != null) line += $" COLLATE {col.Collation}";

                sb.AppendLine(line + ",");
            }

            // الأعمدة المحسوبة
            foreach (var col in columns.Where(c => c.IsComputed))
            {
                sb.AppendLine($"  [{col.Name}] AS ({col.ComputedExpression}),");
            }

            // المفتاح الأساسي المفرد أو المركب
            var pkCols = columns.Where(c => c.IsPrimaryKey).Select(c => $"[{c.Name}]").ToList();
            if (pkCols.Count > 0)
            {
                var pkLine = string.Join(", ", pkCols);
                sb.AppendLine($"  CONSTRAINT [PK_{tableName}] PRIMARY KEY ({pkLine}),");
            }

            // القيود الفريدة
            foreach (var col in columns.Where(c => c.IsUnique && !c.IsPrimaryKey))
            {
                sb.AppendLine($"  CONSTRAINT [UQ_{tableName}_{col.Name}] UNIQUE ([{col.Name}]),");
            }

            // العلاقات الخارجية
            foreach (var col in columns.Where(c => c.ForeignKeyTargetTable != null))
            {
                sb.AppendLine($"  CONSTRAINT [FK_{tableName}_{col.Name}] FOREIGN KEY ([{col.Name}]) REFERENCES [{col.ForeignKeyTargetTable}]([{col.ForeignKeyTargetColumn}]),");
            }

            sb.Remove(sb.Length - 3, 1); // إزالة الفاصلة الأخيرة
            sb.AppendLine(");");

            // الفهارس
            foreach (var col in columns.Where(c => c.HasIndex))
            {
                var indexName = col.IndexName ?? $"IX_{tableName}_{col.Name}";
                var unique = col.IsIndexUnique ? "UNIQUE " : "";
                sb.AppendLine($"CREATE {unique}INDEX [{indexName}] ON [{schema}].[{tableName}] ([{col.Name}]);");
            }

            // التعليقات
            foreach (var col in columns.Where(c => !string.IsNullOrWhiteSpace(c.Comment)))
            {
                sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{col.Comment}',");
                sb.AppendLine($"  @level0type = N'SCHEMA', @level0name = N'{schema}',");
                sb.AppendLine($"  @level1type = N'TABLE',  @level1name = N'{tableName}',");
                sb.AppendLine($"  @level2type = N'COLUMN', @level2name = N'{col.Name}';");
            }

            return sb.ToString();
        }



        /// <summary>
        /// Builds a CREATE TABLE SQL script for the specified <see cref="EntityDefinition"/>, including indexes.
        /// </summary>
        public string Build(EntityDefinition entity)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [{entity.Schema}].[{entity.Name}] (");

            // Columns
            foreach (var col in entity.Columns)
            {
                var line = $"  [{col.Name}] {SqlTypeMapper.Map(col.TypeName)}";

                if (!col.IsNullable) line += " NOT NULL";
                if (!string.IsNullOrWhiteSpace(col.DefaultValue?.ToString())) line += $" DEFAULT {col.DefaultValue}";

                sb.AppendLine(line + ",");
            }

            // Computed Columns
            foreach (var comp in entity.ComputedColumns)
            {
                sb.AppendLine($"  [{comp.Name}] AS ({comp.Expression}),");
            }

            // Primary Key
            if (entity.PrimaryKey?.Columns?.Count > 0)
            {
                var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"  CONSTRAINT [PK_{entity.Name}] PRIMARY KEY ({pkCols}),");
            }

            // Unique Constraints
            foreach (var uc in entity.UniqueConstraints)
            {
                var ucCols = string.Join(", ", uc.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"  CONSTRAINT [UQ_{entity.Name}_{string.Join("_", uc.Columns)}] UNIQUE ({ucCols}),");
            }

            // Foreign Keys
            foreach (var fk in entity.ForeignKeys)
            {
                sb.AppendLine($"  CONSTRAINT [FK_{entity.Name}_{fk.Column}] FOREIGN KEY ([{fk.Column}]) REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]),");
            }

            // Remove trailing comma
            sb.Remove(sb.Length - 3, 1);
            sb.AppendLine(");");

            // Indexes (outside CREATE TABLE)
            foreach (var index in entity.Indexes)
            {
                var unique = index.IsUnique ? "UNIQUE " : "";
                var cols = string.Join(", ", index.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"CREATE {unique}INDEX [{index.Name}] ON [{entity.Schema}].[{entity.Name}] ({cols});");
            }

            return sb.ToString();
        }

    }
}