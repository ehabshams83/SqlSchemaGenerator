using Microsoft.Extensions.Logging;

using Syn.Core.SqlSchemaGenerator.Attributes;

using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Helper;

public static class HelperMethod
{
    internal static readonly List<string> _suppressedWarnings = [];

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

    /// <summary>
    /// Infers SQL type name from CLR type if not explicitly provided.
    /// </summary>
    public static string MapClrTypeToSql(this Type clrType, int? maxLength = null, int? precision = null, int? scale = null)
    {
        // لو النوع Nullable<T> ناخد الـ T
        var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        // 📝 نصوص
        if (underlyingType == typeof(string))
        {
            if (maxLength.HasValue && maxLength.Value > 0)
                return $"nvarchar({maxLength.Value})";
            return "nvarchar(max)";
        }

        if (underlyingType == typeof(char))
        {
            if (maxLength.HasValue && maxLength.Value > 0)
                return $"nchar({maxLength.Value})";
            return "nchar(1)";
        }

        // 🔢 أعداد صحيحة
        if (underlyingType == typeof(byte))
            return "tinyint";

        if (underlyingType == typeof(short))
            return "smallint";

        if (underlyingType == typeof(int))
            return "int";

        if (underlyingType == typeof(long))
            return "bigint";

        if (underlyingType == typeof(sbyte))
            return "tinyint"; // SQL Server مفيهوش signed byte

        if (underlyingType == typeof(ushort))
            return "int"; // مفيش smalluint

        if (underlyingType == typeof(uint))
            return "bigint";

        if (underlyingType == typeof(ulong))
            return "decimal(20,0)"; // علشان ما نفقدش الدقة

        // 🔢 أعداد عشرية
        if (underlyingType == typeof(decimal))
        {
            int p = precision ?? 18;
            int s = scale ?? 2;
            return $"decimal({p},{s})";
        }

        if (underlyingType == typeof(float))
            return "real";

        if (underlyingType == typeof(double))
            return "float";

        // ✅ منطقية
        if (underlyingType == typeof(bool))
            return "bit";

        // 📅 تواريخ وأوقات
        if (underlyingType == typeof(DateTime))
            return "datetime";

        if (underlyingType == typeof(DateTimeOffset))
            return "datetimeoffset";

        if (underlyingType == typeof(TimeSpan))
            return "time(7)";

        // 🆔 معرفات
        if (underlyingType == typeof(Guid))
            return "uniqueidentifier";

        // 📦 بيانات ثنائية
        if (underlyingType == typeof(byte[]))
        {
            if (maxLength.HasValue && maxLength.Value > 0)
                return $"varbinary({maxLength.Value})";
            return "varbinary(max)";
        }

        // 📜 Enums
        if (underlyingType.IsEnum)
            return "int"; // نخزن الـ Enum كـ int

        // أي نوع غير معروف → fallback
        return "nvarchar(max)";
    }

    /// <summary>
    /// Converts a CLR value into a valid SQL literal string representation,
    /// taking into account the target SQL type for proper formatting.
    /// </summary>
    /// <param name="value">
    /// The CLR value to convert. Can be of any supported type such as string, 
    /// numeric types, DateTime, Guid, byte[], etc.
    /// </param>
    /// <param name="sqlTypeName">
    /// The SQL type name (e.g., "nvarchar(50)", "int", "datetime") used to determine 
    /// the correct literal format, such as prefixing Unicode strings with N.
    /// </param>
    /// <returns>
    /// A string containing the SQL literal representation of the value, ready to be 
    /// embedded directly in a SQL statement. Returns "NULL" if the value is null.
    /// </returns>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item>
    /// Escapes single quotes in string and char values, and prefixes Unicode strings with N.
    /// </item>
    /// <item>
    /// Formats DateTime and DateTimeOffset values in ISO 8601 format for SQL Server compatibility.
    /// </item>
    /// <item>
    /// Converts byte arrays to hexadecimal literals (0x...).
    /// </item>
    /// <item>
    /// Uses invariant culture for numeric formatting to avoid locale-specific issues.
    /// </item>
    /// <item>
    /// Provides safe fallbacks for unsupported or unknown types by converting them to strings.
    /// </item>
    /// </list>
    /// </remarks>
    public static string ToSqlLiteral(this object value, string sqlTypeName)
    {
        if (value == null)
            return "NULL";

        // نستخدم InvariantCulture للأرقام
        var ic = CultureInfo.InvariantCulture;
        string lowerType = (sqlTypeName ?? string.Empty).ToLowerInvariant();

        switch (value)
        {
            case string s:
                // لو النوع Unicode (nvarchar/nchar) نخلي N'...'
                var isUnicode = lowerType.StartsWith("nchar") || lowerType.StartsWith("nvarchar");
                var escaped = s.Replace("'", "''");
                return isUnicode ? $"N'{escaped}'" : $"'{escaped}'";

            case char ch:
                var chEsc = ch == '\'' ? "''" : ch.ToString();
                var isNChar = lowerType.StartsWith("nchar");
                return isNChar ? $"N'{chEsc}'" : $"'{chEsc}'";

            case bool b:
                return b ? "1" : "0";

            case byte by:
                return by.ToString(ic);

            case sbyte sby:
                return sby.ToString(ic);

            case short sh:
                return sh.ToString(ic);

            case ushort ush:
                return ush.ToString(ic);

            case int i:
                return i.ToString(ic);

            case uint ui:
                return ui.ToString(ic);

            case long l:
                return l.ToString(ic);

            case ulong ul:
                // SQL Server لا يدعم ulong مباشرة، لكن كـ literal هو رقم صحيح
                return ul.ToString(ic);

            case float f:
                return f.ToString(ic);

            case double d:
                return d.ToString(ic);

            case decimal m:
                return m.ToString(ic);

            case Guid g:
                return $"'{g:D}'";

            case DateTime dt:
                // صيغة ISO 8601 آمنة
                return $"'{dt:yyyy-MM-ddTHH:mm:ss.fff}'";

            case DateTimeOffset dto:
                return $"'{dto:yyyy-MM-ddTHH:mm:ss.fffK}'";

            case TimeSpan ts:
                // time(7)
                return $"'{ts:hh\\:mm\\:ss\\.fffffff}'";

            case byte[] bytes:
                if (bytes.Length == 0) return "0x";
                var hex = "0x" + BitConverter.ToString(bytes).Replace("-", string.Empty);
                return hex;

            default:
                // fallback كسلسلة
                var sdef = Convert.ToString(value, ic) ?? string.Empty;
                sdef = sdef.Replace("'", "''");
                return $"'{sdef}'";
        }
    }

    /// <summary>
    /// Maps matching properties from a source object to a new instance of the destination type.
    /// Supports type conversion, case-insensitive matching, and deep copy for lists.
    /// </summary>
    public static TDestination MapTo<TSource, TDestination>(this TSource source)
        where TDestination : new()
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var destination = new TDestination();

        var sourceProps = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destProps = typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var destProp in destProps)
        {
            if (!destProp.CanWrite) continue;

            // نبحث عن خاصية بنفس الاسم (case-insensitive)
            var sourceProp = sourceProps.FirstOrDefault(p =>
                string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase) &&
                p.CanRead);

            if (sourceProp == null) continue;

            var value = sourceProp.GetValue(source, null);
            if (value == null)
            {
                destProp.SetValue(destination, null);
                continue;
            }

            // لو النوعين متوافقين مباشرة
            if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
            {
                // لو الخاصية List أو ICollection → نعمل نسخة جديدة
                if (value is IList listValue && destProp.PropertyType != typeof(string))
                {
                    var newList = (IList)Activator.CreateInstance(destProp.PropertyType);
                    foreach (var item in listValue)
                        newList.Add(item);
                    destProp.SetValue(destination, newList);
                }
                else
                {
                    destProp.SetValue(destination, value);
                }
            }
            else
            {
                try
                {
                    // محاولة تحويل النوع
                    var convertedValue = Convert.ChangeType(value, destProp.PropertyType);
                    destProp.SetValue(destination, convertedValue);
                }
                catch
                {
                    // لو التحويل فشل، نتجاهل الخاصية
                }
            }
        }

        return destination;
    }

}
