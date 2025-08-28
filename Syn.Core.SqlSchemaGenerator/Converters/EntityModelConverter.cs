using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Converters;


/// <summary>
/// Converts EntityDefinition structures back into EntityModel representations.
/// Useful for reverse engineering or snapshot restoration.
/// </summary>
public static class EntityModelConverter
{
    /// <summary>
    /// Converts an EntityDefinition into an EntityModel.
    /// </summary>
    public static EntityModel ToEntityModel(this EntityDefinition def)
    {
        return new EntityModel
        {
            Name = def.Name,
            Schema = def.Schema,
            Description = def.Description,
            IsIgnored = def.IsIgnored,
            Version = "1.0", // You can override this if versioning is tracked elsewhere

            Columns = def.Columns.Select(c => new ColumnModel
            {
                Name = c.Name,
                TypeName = c.TypeName,
                IsNullable = c.IsNullable,
                DefaultValue = c.DefaultValue,
                Collation = c.Collation,
                Description = c.Description,
                IsUnique = c.IsUnique,
                UniqueConstraintName = c.UniqueConstraintName,
                Indexes = c.Indexes.Select(i => new IndexModel
                {
                    Name = i.Name,
                    IsUnique = i.IsUnique,
                    IncludeColumns = i.Columns,
                    Description = i.Description
                }).ToList(),
                CheckConstraints = c.CheckConstraints.Select(cc => new CheckConstraintModel
                {
                    Name = cc.Name,
                    Expression = cc.Expression,
                    Description = cc.Description
                }).ToList(),
                IsIgnored = c.IsIgnored,
                IgnoreReason = c.IgnoreReason,
                IsPrimaryKey = c.IsPrimaryKey,
                IsForeignKey = c.IsForeignKey,
                ForeignKeyTarget = c.ForeignKeyTarget,
                Order = c.Order,
                MaxLength = ExtractMaxLength(c.TypeName)
            }).ToList(),

            ComputedColumns = def.ComputedColumns.Select(cc => new ColumnModel
            {
                Name = cc.Name,
                TypeName = cc.DataType,
                ComputedExpression = cc.Expression,
                IsPersisted = cc.IsPersisted,
                Description = cc.Description,
                ComputedSource = cc.Source,
                IsIgnored = cc.IsIgnored,
                IgnoreReason = cc.IgnoreReason,
                Order = cc.Order,
                IsComputed = true
            }).ToList(),

            TableIndexes = def.Indexes.Select(i => new IndexModel
            {
                Name = i.Name,
                IsUnique = i.IsUnique,
                IncludeColumns = i.Columns,
                Description = i.Description
            }).ToList(),

            Constraints = def.CheckConstraints.Select(cc => cc.Expression).ToList(),

            Tags = new List<string>(), // Optional: populate if available
            Source = null,
            IsView = false
        };
    }

    /// <summary>
    /// Extracts max length from SQL type string (e.g., nvarchar(100)).
    /// </summary>
    private static int ExtractMaxLength(string typeName)
    {
        if (typeName.StartsWith("nvarchar"))
        {
            var start = typeName.IndexOf('(');
            var end = typeName.IndexOf(')');
            if (start > 0 && end > start)
            {
                var lengthStr = typeName.Substring(start + 1, end - start - 1);
                return lengthStr == "max" ? -1 : int.TryParse(lengthStr, out var len) ? len : 0;
            }
        }
        return 0;
    }
}