namespace Syn.Core.SqlSchemaGenerator.Converters;

using Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Provides extension methods to convert <see cref="EntityDefinition"/> into <see cref="EntityModel"/>.
/// Ensures full compatibility and metadata preservation for schema generation.
/// </summary>
public static class EntityDefinitionConverter
{
    /// <summary>
    /// Converts an <see cref="EntityDefinition"/> into a fully populated <see cref="EntityModel"/>,
    /// including regular and computed columns, constraints, and indexes.
    /// </summary>
    /// <param name="def">The source entity definition.</param>
    /// <returns>A compatible <see cref="EntityModel"/> instance.</returns>
    public static EntityModel ToEntityModel(this EntityDefinition def)
    {
        var columns = def.Columns.Select(c => new ColumnModel
        {
            Name = c.Name,
            TypeName = c.TypeName,
            PropertyType = typeof(string),
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
                Columns = new List<string> { c.Name }
            }).ToList(),
            CheckConstraints = c.CheckConstraints.Select(cc => new CheckConstraintModel
            {
                Name = cc.Name,
                Expression = cc.Expression
            }).ToList(),
            IsIgnored = c.IsIgnored,
            IgnoreReason = c.IgnoreReason,
            IsComputed = false,
            ComputedExpression = null,
            ComputedSource = null,
            IsPersisted = false,
            IsPrimaryKey = c.IsPrimaryKey,
            IsForeignKey = c.IsForeignKey,
            ForeignKeyTarget = c.ForeignKeyTarget,
            Order = c.Order
        }).ToList();

        var computedColumns = def.ComputedColumns.Select(c => new ColumnModel
        {
            Name = c.Name,
            TypeName = c.DataType,
            PropertyType = typeof(string),
            IsComputed = true,
            ComputedExpression = c.Expression,
            ComputedSource = c.Source,
            IsPersisted = c.IsPersisted,
            Description = c.Description,
            IsIgnored = c.IsIgnored,
            IgnoreReason = c.IgnoreReason,
            IsNullable = true,
            IsUnique = false,
            Indexes = new List<IndexModel>(),
            CheckConstraints = new List<CheckConstraintModel>(),
            DefaultValue = null,
            Collation = null,
            UniqueConstraintName = null,
            IsPrimaryKey = false,
            IsForeignKey = false,
            ForeignKeyTarget = null,
            Order = c.Order
        }).ToList();

        columns.AddRange(computedColumns);

        return new EntityModel
        {
            Name = def.Name,
            Schema = def.Schema,
            Version = "1.0", // ثابت أو ممكن نقرأه من مكان خارجي
            Description = def.Description,
            Columns = columns,
            ComputedColumns = computedColumns,
            Constraints = def.CheckConstraints.Select(c => c.Name).ToList(),
            TableIndexes = def.Indexes.Select(i => new IndexModel
            {
                Name = i.Name,
                IsUnique = i.IsUnique,
                Columns = i.Columns.ToList()
            }).ToList(),
            IsIgnored = def.IsIgnored,
            Source = "Definition",
            Tags = new List<string>(),
            IsView = false
        };
    }
}