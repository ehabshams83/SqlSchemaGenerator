using System;
using System.Collections.Generic;
using System.Linq;
using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Converters
{
    /// <summary>
    /// Converts high-level EntityModel objects into SQL-ready EntityDefinition structures.
    /// </summary>
    public static class EntityDefinitionConverter
    {
        /// <summary>
        /// Converts an EntityModel into an EntityDefinition for SQL generation.
        /// </summary>
        public static EntityDefinition ToEntityDefinition(this EntityModel model)
        {
            return new EntityDefinition
            {
                Name = model.Name,
                Schema = model.Schema,
                Description = model.Description,
                IsIgnored = model.IsIgnored,

                Columns = model.Columns.Select(c => new ColumnDefinition
                {
                    Name = c.Name,
                    TypeName = c.TypeName ?? MapClrTypeToSql(c.PropertyType),
                    IsNullable = c.IsNullable,
                    DefaultValue = c.DefaultValue,
                    Collation = c.Collation,
                    Description = c.Description,
                    IsUnique = c.IsUnique,
                    UniqueConstraintName = c.UniqueConstraintName,
                    Indexes = c.Indexes.Select(i => new IndexDefinition
                    {
                        Name = i.Name ?? $"IX_{model.Name}_{c.Name}",
                        Columns = new List<string> { c.Name },
                        IsUnique = i.IsUnique,
                        Description = i.Description
                    }).ToList(),
                    CheckConstraints = c.CheckConstraints.Select(cc => new CheckConstraintDefinition
                    {
                        Name = cc.Name ?? $"CK_{model.Name}_{c.Name}",
                        Expression = cc.Expression,
                        Description = cc.Description
                    }).ToList(),
                    IsIgnored = c.IsIgnored,
                    IgnoreReason = c.IgnoreReason,
                    IsPrimaryKey = c.IsPrimaryKey,
                    IsForeignKey = c.IsForeignKey,
                    ForeignKeyTarget = c.ForeignKeyTarget,
                    Order = c.Order
                }).ToList(),

                ComputedColumns = model.ComputedColumns.Select(cc => ComputedColumnConverter.ToComputedColumnDefinition(cc)).ToList(),

                Indexes = model.TableIndexes.Select(i => new IndexDefinition
                {
                    Name = i.Name ?? $"IX_{model.Name}_{string.Join("_", i.IncludeColumns)}",
                    Columns = i.IncludeColumns,
                    IsUnique = i.IsUnique,
                    Description = i.Description
                }).ToList(),

                CheckConstraints = model.Constraints.Select(expr => new CheckConstraintDefinition
                {
                    Name = $"CK_{model.Name}_{Guid.NewGuid():N}",
                    Expression = expr
                }).ToList()
            };
        }

        private static string MapClrTypeToSql(Type type)
        {
            // Simplified mapping logic
            if (type == typeof(string)) return "nvarchar(max)";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bit";
            if (type == typeof(DateTime)) return "datetime";
            if (type == typeof(decimal)) return "decimal(18,2)";
            return "nvarchar(max)";
        }
    }
}