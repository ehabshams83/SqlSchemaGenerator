using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator
{
    public class SqlTableScriptBuilder
    {
        /// <summary>
        /// Builds an EntityDefinition directly from a CLR Type.
        /// </summary>
        public EntityDefinition Build(Type entityType)
        {
            var (schema, table) = entityType.GetTableInfo();

            var entity = new EntityDefinition
            {
                Name = table,
                Schema = schema
            };

            // Table description from attribute (optional)
            var tableDescAttr = entityType.GetCustomAttribute<DescriptionAttribute>();
            if (tableDescAttr != null && !string.IsNullOrWhiteSpace(tableDescAttr.Description))
                entity.Description = tableDescAttr.Description;

            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var columnDef = new ColumnDefinition
                {
                    Name = GetColumnName(prop),
                    TypeName = MapClrTypeToSql(prop.PropertyType, out int? prec, out int? scale),
                    Precision = prec,
                    Scale = scale,
                    IsNullable = IsNullable(prop),
                    DefaultValue = GetDefaultValue(prop),
                    Collation = GetCollation(prop),
                    Description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    IsIdentity = HasIdentityAttribute(prop),
                    Order = GetOrder(prop)
                };

                // Computed column handling
                var computedExpr = GetComputedExpression(prop);
                if (!string.IsNullOrWhiteSpace(computedExpr))
                {
                    entity.ComputedColumns.Add(new ComputedColumnDefinition
                    {
                        Name = columnDef.Name,
                        Expression = computedExpr
                    });
                }
                else
                {
                    entity.Columns.Add(columnDef);
                }

                // Check constraints (if any attribute found)
                entity.CheckConstraints.AddRange(GetCheckConstraints(prop, entity.Name));

                // Indexes (attribute driven)
                entity.Indexes.AddRange(GetIndexes(prop, entity.Name));
            }

            // Keys & constraints
            entity.PrimaryKey = GetPrimaryKey(entityType);
            entity.UniqueConstraints = GetUniqueConstraints(entityType);

            // Foreign keys + default naming if missing
            entity.ForeignKeys = entityType.GetForeignKeys();
            foreach (var fk in entity.ForeignKeys)
            {
                if (string.IsNullOrWhiteSpace(fk.ConstraintName))
                    fk.ConstraintName = $"FK_{entity.Name}_{fk.Column}";
            }
            ValidateForeignKeys(entity);

            // Class-level indexes
            entity.Indexes.AddRange(GetIndexes(entityType));
            entity.Indexes = entity.Indexes
                .GroupBy(ix => ix.Name)
                .Select(g => g.First())
                .ToList();

            return entity;
        }

        /// <summary>
        /// Builds a CREATE TABLE SQL script from an EntityDefinition.
        /// </summary>
        public string Build(EntityDefinition entity)
        {
            if (entity == null || entity.IsIgnored)
                return string.Empty;

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            var sb = new StringBuilder();

            sb.AppendLine($"CREATE TABLE [{schema}].[{entity.Name}] (");

            // الأعمدة الفعلية
            foreach (var col in entity.Columns)
            {
                var line = $"  [{col.Name}] {col.TypeName}";

                if (col.Precision.HasValue && col.Scale.HasValue)
                    line += $"({col.Precision},{col.Scale})";

                if (!string.IsNullOrWhiteSpace(col.Collation))
                    line += $" COLLATE {col.Collation}";

                line += col.IsNullable ? " NULL" : " NOT NULL";

                if (col.IsIdentity)
                    line += " IDENTITY(1,1)";

                if (col.DefaultValue != null && !string.IsNullOrWhiteSpace(col.DefaultValue.ToString()))
                    line += $" DEFAULT {col.DefaultValue}";

                sb.AppendLine(line + ",");
            }

            // الأعمدة المحسوبة
            foreach (var comp in entity.ComputedColumns)
            {
                sb.AppendLine($"  [{comp.Name}] AS ({comp.Expression}),");
            }

            // المفتاح الأساسي
            if (entity.PrimaryKey?.Columns?.Count > 0)
            {
                var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"  CONSTRAINT [{entity.PrimaryKey.Name}] PRIMARY KEY ({pkCols}),");
            }

            // قيود UNIQUE
            foreach (var uc in entity.UniqueConstraints)
            {
                var cols = string.Join(", ", uc.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"  CONSTRAINT [{uc.Name}] UNIQUE ({cols}),");
            }

            // المفاتيح الأجنبية
            foreach (var fk in entity.ForeignKeys)
            {
                var fkName = !string.IsNullOrWhiteSpace(fk.ConstraintName)
                    ? fk.ConstraintName
                    : $"FK_{entity.Name}_{fk.Column}";

                var onDelete = fk.OnDelete switch
                {
                    ReferentialAction.Cascade => " ON DELETE CASCADE",
                    ReferentialAction.SetNull => " ON DELETE SET NULL",
                    ReferentialAction.SetDefault => " ON DELETE SET DEFAULT",
                    _ => ""
                };

                var onUpdate = fk.OnUpdate switch
                {
                    ReferentialAction.Cascade => " ON UPDATE CASCADE",
                    ReferentialAction.SetNull => " ON UPDATE SET NULL",
                    ReferentialAction.SetDefault => " ON UPDATE SET DEFAULT",
                    _ => ""
                };

                sb.AppendLine($"  CONSTRAINT [{fkName}] FOREIGN KEY ([{fk.Column}]) REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]){onDelete}{onUpdate},");
            }

            // إزالة آخر فاصلة
            if (sb[sb.Length - 3] == ',')
                sb.Remove(sb.Length - 3, 1);

            sb.AppendLine(");");

            // قيود CHECK
            foreach (var check in entity.CheckConstraints)
            {
                sb.AppendLine($"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{check.Name}] CHECK ({check.Expression});");
            }

            // الفهارس
            foreach (var index in entity.Indexes)
            {
                var unique = index.IsUnique ? "UNIQUE " : "";
                var cols = string.Join(", ", index.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"CREATE {unique}INDEX [{index.Name}] ON [{schema}].[{entity.Name}] ({cols});");
            }

            // وصف الجدول
            if (!string.IsNullOrWhiteSpace(entity.Description))
            {
                sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{entity.Description}', @level0type = N'SCHEMA', @level0name = N'{schema}', @level1type = N'TABLE', @level1name = N'{entity.Name}';");
            }

            // أوصاف الأعمدة
            foreach (var col in entity.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Description) || !string.IsNullOrWhiteSpace(c.Comment)))
            {
                var desc = col.Description ?? col.Comment;
                sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{desc}', @level0type = N'SCHEMA', @level0name = N'{schema}', @level1type = N'TABLE', @level1name = N'{entity.Name}', @level2type = N'COLUMN', @level2name = N'{col.Name}';");
            }

            return sb.ToString();
        }


        /// <summary>
        /// Shortcut: Builds a CREATE TABLE SQL script directly from a CLR Type.
        /// </summary>
        public string BuildScriptFromType(Type entityType)
        {
            var entity = Build(entityType);
            return Build(entity);
        }

        // ===== Helper methods below =====
        private static string GetColumnName(PropertyInfo prop) => prop.Name;
        private static bool IsNullable(PropertyInfo prop) => !prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null;
        private static object? GetDefaultValue(PropertyInfo prop) => null; // add logic if needed
        private static string? GetCollation(PropertyInfo prop) => null;
        private static bool HasIdentityAttribute(PropertyInfo prop)
        {
            var dbGenAttr = prop.GetCustomAttribute<DatabaseGeneratedAttribute>();
            return dbGenAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;
        }
        private static int? GetOrder(PropertyInfo prop) => null;
        private static string? GetComputedExpression(PropertyInfo prop) => null;
        private static IEnumerable<CheckConstraintDefinition> GetCheckConstraints(PropertyInfo prop, string tableName) => Array.Empty<CheckConstraintDefinition>();
        private static IEnumerable<IndexDefinition> GetIndexes(PropertyInfo prop, string tableName) => Array.Empty<IndexDefinition>();
        private static PrimaryKeyDefinition GetPrimaryKey(Type type) => null;
        private static List<UniqueConstraintDefinition> GetUniqueConstraints(Type type) => new();
        private static List<IndexDefinition> GetIndexes(Type type) => new();
        private static void ValidateForeignKeys(EntityDefinition entity) { }
        private static string MapClrTypeToSql(Type clrType, out int? precision, out int? scale)
        {
            precision = null; scale = null;
            // basic mapping example
            if (clrType == typeof(string)) return "nvarchar";
            if (clrType == typeof(int)) return "int";
            if (clrType == typeof(decimal)) { precision = 18; scale = 2; return "decimal"; }
            return "nvarchar";
        }
    }
}