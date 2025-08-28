using Microsoft.Extensions.Logging;

using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Syn.Core.SqlSchemaGenerator.Helper
{
    public static class HelperMethod
    {
        /// <summary>
        /// Extracts the table name and schema from SqlTableAttribute or TableAttribute, or defaults to type name and 'dbo'.
        /// Logs a warning if both attributes are present.
        /// </summary>
        /// <param name="type">The entity type to inspect.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <returns>A tuple of (schemaName, tableName).</returns>
        public static (string Schema, string Table) GetTableInfo(this Type type)
        {
            var sqlTableAttr = type.GetCustomAttribute<SqlTableAttribute>();
            var tableAttr = type.GetCustomAttribute<TableAttribute>();

            // Log warning if both attributes are present
            if (sqlTableAttr != null && tableAttr != null)
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder
                        .AddConsole()
                        .SetMinimumLevel(LogLevel.Warning); // أو Debug لو عاوز تفاصيل أكتر
                });
                ILogger logger = loggerFactory.CreateLogger("SqlSchemaGenerator");

                logger?.LogWarning(
                    "Type '{TypeName}' has both [SqlTable] and [Table] attributes. [SqlTable] will take precedence.",
                    type.FullName
                );
            }

            if (sqlTableAttr != null)
            {
                return (sqlTableAttr.Schema, sqlTableAttr.Name);
            }

            var tableName = tableAttr?.Name?.Trim();
            var schemaName = tableAttr?.Schema?.Trim();

            tableName ??= type.Name;
            schemaName ??= "dbo";

            return (schemaName, tableName);
        }


        public static (string TableName, string Schema) ParseTableInfo(this Type entityType)
        {
            var customAttr = entityType.GetCustomAttribute<TableAttribute>();
            var efAttr = entityType.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();

            var name = customAttr?.Name ?? efAttr?.Name ?? entityType.Name;
            var schema = customAttr?.Schema ?? efAttr?.Schema ?? "dbo";

            return (name, schema);
        }

        /// <summary>
        /// Extracts foreign key relationships from [ForeignKey] attributes.
        /// </summary>
        public static List<ForeignKeyDefinition> GetForeignKeys(this Type type)
        {
            var foreignKeys = new List<ForeignKeyDefinition>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // أولاً: الاتربيوت المخصص بتاعك
                var customAttr = prop.GetCustomAttribute<Syn.Core.SqlSchemaGenerator.Attributes.ForeignKeyAttribute>();
                if (customAttr != null)
                {
                    foreignKeys.Add(new ForeignKeyDefinition
                    {
                        Column = prop.Name,
                        ReferencedTable = customAttr.TargetTable,
                        ReferencedColumn = customAttr.TargetColumn,
                        OnDelete = ReferentialAction.Cascade,
                        OnUpdate = ReferentialAction.NoAction
                    });

                    continue; // لو لقينا الاتربيوت المخصص، نتجاهل الاتربيوت القياسي
                }

                // ثانيًا: الاتربيوت القياسي بتاع EF
                var efAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute>();
                if (efAttr != null)
                {
                    foreignKeys.Add(new ForeignKeyDefinition
                    {
                        Column = prop.Name,
                        ReferencedTable = efAttr.Name,
                        ReferencedColumn = "Id", // قابلة للتوسعة لاحقًا
                        OnDelete = ReferentialAction.Cascade,
                        OnUpdate = ReferentialAction.NoAction
                    });
                }
            }

            return foreignKeys;
        }


        public static string GenerateForeignKeyConstraints(string schema, string tableName, List<ForeignKeyDefinition> foreignKeys)
        {
            var sb = new StringBuilder();

            foreach (var fk in foreignKeys)
            {
                var constraintName = $"FK_{tableName}_{fk.Column}";
                var onDelete = fk.OnDelete switch
                {
                    ReferentialAction.Cascade => "ON DELETE CASCADE",
                    ReferentialAction.SetNull => "ON DELETE SET NULL",
                    ReferentialAction.SetDefault => "ON DELETE SET DEFAULT",
                    _ => ""
                };

                var onUpdate = fk.OnUpdate switch
                {
                    ReferentialAction.Cascade => "ON UPDATE CASCADE",
                    ReferentialAction.SetNull => "ON UPDATE SET NULL",
                    ReferentialAction.SetDefault => "ON UPDATE SET DEFAULT",
                    _ => ""
                };

                sb.AppendLine($@"ALTER TABLE [{schema}].[{tableName}] 
ADD CONSTRAINT [{constraintName}] 
FOREIGN KEY ([{fk.Column}]) 
REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]) 
{onDelete} {onUpdate};");
            }

            return sb.ToString();
        }


        /// <summary>
        /// Extracts composite index definitions from a given entity type.
        /// Supports multiple columns per index and uniqueness.
        /// </summary>
        /// <param name="type">The entity type to inspect.</param>
        /// <returns>List of composite index definitions.</returns>
        //public static List<IndexDefinition> GetCompositeIndexes(this Type type)
        //{
        //    var indexes = new List<IndexDefinition>();

        //    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        //    {
        //        // Check for single-column index attribute
        //        var singleIndexAttr = prop.GetCustomAttribute<IndexAttribute>();
        //        if (singleIndexAttr != null)
        //        {
        //            indexes.Add(new IndexDefinition
        //            {
        //                Name = singleIndexAttr.Name ?? $"IX_{type.Name}_{prop.Name}",
        //                Columns = new List<string> { prop.Name },
        //                IsUnique = singleIndexAttr.IsUnique
        //            });
        //        }

        //        // Check for composite index attribute
        //        var compositeAttrs = prop.GetCustomAttributes<CompositeIndexAttribute>();
        //        foreach (var attr in compositeAttrs)
        //        {
        //            var existing = indexes.FirstOrDefault(ix => ix.Name == attr.Name);
        //            if (existing == null)
        //            {
        //                existing = new IndexDefinition
        //                {
        //                    Name = attr.Name,
        //                    IsUnique = attr.IsUnique
        //                };
        //                indexes.Add(existing);
        //            }

        //            if (!existing.Columns.Contains(prop.Name))
        //                existing.Columns.Add(prop.Name);
        //        }
        //    }

        //    return indexes;
        //}


        public static bool HasIdentityAttribute(this PropertyInfo prop)
        {
            var dbGenAttr = prop.GetCustomAttribute<DatabaseGeneratedAttribute>();
            return dbGenAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;
        }



        public static string FormatDefaultValue(object defaultValue)
        {
            if (defaultValue == null)
                return "NULL";

            // لو القيمة دالة SQL أو تعبير بدون علامات اقتباس
            if (defaultValue is string strVal)
            {
                var trimmed = strVal.Trim();

                // لو النص بيبدأ وينتهي بقوس أو معروف إنه دالة
                if (trimmed.EndsWith(")") || trimmed.Contains("("))
                    return trimmed;

                // إرجاع النص بين أقواس مفردة مع استبدال أي ' بـ ''
                return $"'{trimmed.Replace("'", "''")}'";
            }

            // أي نوع رقمى أو منطقى
            return Convert.ToString(defaultValue, System.Globalization.CultureInfo.InvariantCulture);
        }


        public static T? GetPropertyValue<T>(this object obj, string propertyName)
        {
            var prop = obj.GetType().GetProperty(propertyName);
            return prop != null ? (T?)prop.GetValue(obj) : default;
        }



    }
}
