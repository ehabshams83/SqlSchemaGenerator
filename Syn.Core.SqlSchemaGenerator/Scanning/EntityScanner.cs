using Microsoft.EntityFrameworkCore.Metadata.Internal;

using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;

using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Scanning;

/// <summary>
/// Scans assemblies for entity types and builds their schema definitions.
/// </summary>
public class EntityScanner
{
    private readonly EntityDefinitionBuilder _builder;

    /// <summary>
    /// Initializes a new instance of the EntityScanner.
    /// </summary>
    /// <param name="builder">The builder used to construct entity definitions.</param>
    public EntityScanner(EntityDefinitionBuilder builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// Scans the given assemblies for entity types and returns their definitions.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>A list of discovered entity definitions.</returns>
    public List<EntityDefinition> Scan(params Assembly[] assemblies)
    {
        var entities = new List<EntityDefinition>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!IsValidEntity(type))
                    continue;

                var entity = _builder
                .BuildAllWithRelationships(new[] { type })
                .First();

                if (!entity.IsIgnored)
                    entities.Add(entity);
            }
        }

        return entities;
    }

    /// <summary>
    /// Determines whether a type is a valid entity for scanning.
    /// </summary>
    private static bool IsValidEntity(Type type)
    {
        return type.IsClass &&
               !type.IsAbstract &&
               type.IsPublic &&
               type.GetCustomAttribute<NotMappedAttribute>() is null;
    }
}