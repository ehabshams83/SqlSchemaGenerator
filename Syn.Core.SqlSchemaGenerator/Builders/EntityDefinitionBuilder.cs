using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.AttributeHandlers;
using Syn.Core.SqlSchemaGenerator.Migrations.AlterTable;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Builders;

/// <summary>
/// Builds an EntityDefinition from a CLR type by applying schema attribute handlers.
/// </summary>
public class EntityDefinitionBuilder
{
    private readonly IEnumerable<ISchemaAttributeHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the EntityDefinitionBuilder.
    /// </summary>
    /// <param name="handlers">The attribute handlers to apply to each property.</param>
    public EntityDefinitionBuilder(IEnumerable<ISchemaAttributeHandler> handlers)
    {
        _handlers = handlers;
    }

    /// <summary>
    /// Builds an EntityDefinition from the specified CLR type.
    /// </summary>
    /// <param name="entityType">The CLR type representing the entity.</param>
    /// <returns>The constructed EntityDefinition.</returns>
    public EntityDefinition Build(Type entityType)
    {
        var entity = new EntityDefinition
        {
            Name = entityType.Name,
            Schema = "dbo"
        };

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var columnModel = new ColumnModel
            {
                Name = prop.Name,
                PropertyType = prop.PropertyType
            };

            foreach (var handler in _handlers)
            {
                handler.Apply(prop, columnModel);
            }

            if (columnModel.IsIgnored)
                continue;

            // Column definition
            var columnDef = ToColumnDefinition(columnModel);
            entity.Columns.Add(columnDef);

            // Computed column
            var computed = ToComputed(columnModel);
            if (computed is not null)
                entity.ComputedColumns.Add(computed);

            // Check constraints
            var checks = ToCheckConstraints(columnModel, entity.Name);
            entity.CheckConstraints.AddRange(checks);

            // Indexes
            var indexes = ToIndexes(columnModel, entity.Name);
            entity.Indexes.AddRange(indexes);
        }

        return entity;
    }

    /// <summary>
    /// Converts a ColumnModel to a ColumnDefinition.
    /// </summary>
    private static ColumnDefinition ToColumnDefinition(ColumnModel model)
    {
        return new ColumnDefinition
        {
            Name = model.Name,

            TypeName = model.TypeName ?? InferSqlType(model.PropertyType),
            IsNullable = model.IsNullable,
            DefaultValue = model.DefaultValue,
            Collation = model.Collation,
            Description = model.Description
        };
    }

    /// <summary>
    /// Converts a ColumnModel to a ComputedColumnDefinition if applicable.
    /// </summary>
    private static ComputedColumnDefinition? ToComputed(ColumnModel model)
    {
        if (!model.IsComputed || string.IsNullOrWhiteSpace(model.ComputedExpression))
            return null;

        return new ComputedColumnDefinition
        {
            Name = model.Name,
            Expression = model.ComputedExpression
        };
    }

    /// <summary>
    /// Converts check constraints from ColumnModel to CheckConstraintDefinition.
    /// </summary>
    private static IEnumerable<CheckConstraintDefinition> ToCheckConstraints(ColumnModel model, string entityName)
    {
        return model.CheckConstraints.Select(c => new CheckConstraintDefinition
        {
            Name = c.Name ?? $"CK_{entityName}_{model.Name}_{Guid.NewGuid().ToString("N")[..6]}",
            Expression = c.Expression
        });
    }

    /// <summary>
    /// Converts index definitions from ColumnModel to IndexDefinition.
    /// </summary>
    private static IEnumerable<IndexDefinition> ToIndexes(ColumnModel model, string entityName)
    {
        var indexes = model.Indexes.Select(i => new IndexDefinition
        {
            Name = i.Name ?? $"IX_{entityName}_{model.Name}",
            Columns = new List<string> { model.Name },
            IsUnique = i.IsUnique
        }).ToList();

        if (model.IsUnique)
        {
            indexes.Add(new IndexDefinition
            {
                Name = model.UniqueConstraintName ?? $"UQ_{entityName}_{model.Name}",
                Columns = new List<string> { model.Name },
                IsUnique = true
            });
        }

        return indexes;
    }

    /// <summary>
    /// Infers SQL type name from CLR type if not explicitly provided.
    /// </summary>
    private static string InferSqlType(Type clrType)
    {
        return clrType switch
        {
            Type t when t == typeof(string) => "nvarchar(max)",
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(bool) => "bit",
            Type t when t == typeof(DateTime) => "datetime",
            Type t when t == typeof(decimal) => "decimal(18,2)",
            Type t when t == typeof(Guid) => "uniqueidentifier",
            _ => "nvarchar(max)" // fallback
        };
    }
}