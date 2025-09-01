using Microsoft.Extensions.Logging;

using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
