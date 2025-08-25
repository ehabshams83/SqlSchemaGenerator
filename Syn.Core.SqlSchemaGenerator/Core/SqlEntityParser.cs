using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


//using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Core
{
    /// <summary>
    /// Parses a .NET entity type into a list of SQL column definitions.
    /// </summary>
    public class SqlEntityParser
    {
        /// <summary>
        /// Parses the specified CLR entity type and extracts column metadata for SQL generation.
        /// Supports both custom attributes and EF-compatible attributes, and fills all extended properties.
        /// </summary>
        /// <param name="entityType">The entity type to parse.</param>
        /// <returns>A list of <see cref="SqlColumnInfo"/> representing the parsed columns.</returns>
        public List<SqlColumnInfo> Parse(Type entityType)
        {
            var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var columns = new List<SqlColumnInfo>();

            foreach (var prop in props)
            {
                // Column name
                var efColAttr = prop.GetCustomAttribute<ColumnAttribute>();
                var colName = efColAttr?.Name ?? prop.Name;

                // SQL type name
                var typeName =  efColAttr?.TypeName ?? SqlTypeMapper.Map(prop.PropertyType);

                
                // Nullable
                var isNullable = (!prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null);

                // Default value
                var defaultValue = prop.GetCustomAttribute<Syn.Core.SqlSchemaGenerator.Attributes.DefaultValueAttribute>()?.Value?.ToString();

                // Primary Key
                var isPrimaryKey =
                    prop.GetCustomAttribute<KeyAttribute>() != null ||
                    prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null;

                // Unique
                var isUnique = prop.GetCustomAttribute<Attributes.UniqueAttribute>() != null;

                // Computed
                var customCompAttr = prop.GetCustomAttribute<Attributes.ComputedAttribute>();
                var efDbGenAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute>();
                var isComputed = customCompAttr != null || (efDbGenAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed);
                var computedExpression = customCompAttr?.SqlExpression;

                // Identity
                var isIdentity = efDbGenAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;

                // Foreign Key
                var customFkAttr = prop.GetCustomAttribute<Attributes.ForeignKeyAttribute>();
                var efFkAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute>();
                var foreignTable = customFkAttr?.TargetTable ?? efFkAttr?.Name;
                var foreignColumn = customFkAttr?.TargetColumn ?? "Id";

                // Index
                var indexAttr = prop.GetCustomAttribute<Attributes.IndexAttribute>();
                var hasIndex = indexAttr != null;
                var isIndexUnique = indexAttr?.IsUnique ?? false;
                var indexName = indexAttr?.Name;

                // Comment
                var commentAttr = prop.GetCustomAttribute<Attributes.CommentAttribute>();
                var comment = commentAttr?.Text;

                // Precision / Scale
                var precisionAttr = prop.GetCustomAttribute<Attributes.PrecisionAttribute>();
                var precision = precisionAttr?.Precision;
                var scale = precisionAttr?.Scale;

                // Collation
                var collationAttr = prop.GetCustomAttribute<Attributes.CollationAttribute>();
                var collation = collationAttr?.Name;

                

                columns.Add(new SqlColumnInfo
                {
                    Name = colName,
                    Type = prop.PropertyType,
                    TypeName = typeName,
                    IsNullable = isNullable,
                    DefaultValue = defaultValue,
                    IsPrimaryKey = isPrimaryKey,
                    IsUnique = isUnique,
                    IsComputed = isComputed,
                    ComputedExpression = computedExpression,
                    IsIdentity = isIdentity,
                    ForeignKeyTargetTable = foreignTable,
                    ForeignKeyTargetColumn = foreignColumn,
                    HasIndex = hasIndex,
                    IsIndexUnique = isIndexUnique,
                    IndexName = indexName,
                    Comment = comment,
                    Precision = precision,
                    Scale = scale,
                    Collation = collation
                });
            }

            return columns;
        }

        //public List<SqlColumnInfo> Parse(Type type)
        //{
        //    var columns = new List<SqlColumnInfo>();

        //    foreach (var prop in type.GetProperties())
        //    {
        //        var col = new SqlColumnInfo
        //        {
        //            Name = prop.Name,
        //            Type = prop.PropertyType,
        //            IsNullable = IsNullable(prop),
        //            IsPrimaryKey = prop.GetCustomAttribute<KeyAttribute>() != null,
        //            IsUnique = prop.GetCustomAttribute<UniqueAttribute>() != null,
        //            IsComputed = prop.GetCustomAttribute<ComputedAttribute>() != null,
        //            ComputedExpression = prop.GetCustomAttribute<ComputedAttribute>()?.SqlExpression,
        //            DefaultValue = prop.GetCustomAttribute<DefaultValueAttribute>()?.Value?.ToString()
        //        };

        //        // دعم EF ForeignKeyAttribute
        //        var efFkAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute>();
        //        if (efFkAttr != null)
        //        {
        //            var navigationProp = type.GetProperty(efFkAttr.Name);
        //            if (navigationProp != null)
        //            {
        //                col.ForeignKeyTargetTable = navigationProp.PropertyType.Name;
        //                col.ForeignKeyTargetColumn = "Id"; // نفترض المفتاح الأساسي
        //            }
        //        }

        //        // دعم attribute خاص بنا (لو موجود)
        //        var customFkAttr = prop.GetCustomAttribute<ForeignKeyAttribute>(); // من مكتبتك الخاصة
        //        if (customFkAttr != null)
        //        {
        //            col.ForeignKeyTargetTable = customFkAttr.Name;
        //            col.ForeignKeyTargetColumn = customFkAttr.TargetColumn;
        //        }

        //        columns.Add(col);
        //    }

        //    return columns;

        //}

        private bool IsNullable(PropertyInfo prop)
        {
            var type = prop.PropertyType;
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }


}