using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.AttributeHandlers;

namespace Syn.Core.SqlSchemaGenerator;

/// <summary>
/// Builds schema models from annotated entity types using registered attribute handlers.
/// </summary>
public class SchemaBuilder
{
    private readonly List<ISchemaAttributeHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of <see cref="SchemaBuilder"/> with default handlers.
    /// </summary>
    public SchemaBuilder()
    {
        _handlers = new List<ISchemaAttributeHandler>
        {
            new IndexAttributeHandler(),
            new UniqueAttributeHandler(),
            new DefaultValueAttributeHandler(),
            new CheckConstraintAttributeHandler(),
            new CollationAttributeHandler(),
            new IgnoreColumnAttributeHandler(),
            new ComputedAttributeHandler(),
            new DescriptionAttributeHandler()
        };
    }

    /// <summary>
    /// Builds an <see cref="EntityModel"/> from the specified entity type.
    /// </summary>
    /// <param name="entityType">The entity type to scan.</param>
    /// <returns>The generated entity model.</returns>
    public EntityModel Build(Type entityType)
    {
        var entity = new EntityModel
        {
            Name = entityType.Name,
            Schema = "dbo", // ممكن نخليها قابلة للتعديل لاحقًا
            Columns = new List<ColumnModel>()
        };

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var column = new ColumnModel
            {
                Name = prop.Name,
                TypeName = prop.PropertyType.Name,
                IsNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null
            };

            foreach (var handler in _handlers)
            {
                handler.Apply(prop, column);
            }

            if (!column.IsIgnored)
                entity.Columns.Add(column);
        }

        return entity;
    }
}