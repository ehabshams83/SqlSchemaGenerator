using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Services;

/// <summary>
/// Provides a high-level service to build a fully populated <see cref="EntityDefinition"/>
/// by combining metadata from code attributes/handlers with actual database schema details.
/// </summary>
public class EntityDefinitionService
{
    private readonly EntityDefinitionBuilder _builder;
    private readonly DatabaseSchemaReader _schemaReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityDefinitionService"/>.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="EntityDefinitionBuilder"/> used to construct entities from CLR types.
    /// </param>
    /// <param name="schemaReader">
    /// The <see cref="DatabaseSchemaReader"/> used to enrich entities with DB constraints/checks.
    /// Must be initialized with a valid <see cref="DbConnection"/>.
    /// </param>
    public EntityDefinitionService(EntityDefinitionBuilder builder, DatabaseSchemaReader schemaReader)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _schemaReader = schemaReader ?? throw new ArgumentNullException(nameof(schemaReader));
    }

    /// <summary>
    /// Builds a complete <see cref="EntityDefinition"/> for the given CLR type,
    /// enriched with primary key, unique, foreign key, and check constraints from the database.
    /// </summary>
    /// <param name="entityType">The CLR type representing the entity.</param>
    /// <returns>
    /// A fully populated <see cref="EntityDefinition"/> ready for schema comparison or migration.
    /// </returns>
    public EntityDefinition BuildFull(Type entityType)
    {
        // 1) Build definition from code (attributes, handlers)
        var entity = _builder.BuildAllWithRelationships(new[] { entityType }).First();

        // 2) Enrich with actual DB constraints and checks
        _schemaReader.ReadConstraintsAndChecks(entity);

        return entity;
    }

    /// <summary>
    /// Builds full definitions for a collection of CLR entity types.
    /// </summary>
    /// <param name="entityTypes">Collection of CLR types to build from.</param>
    /// <returns>
    /// List of populated <see cref="EntityDefinition"/> objects.
    /// </returns>
    public List<EntityDefinition> BuildFull(IEnumerable<Type> entityTypes)
    {
        if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));

        var list = new List<EntityDefinition>();
        foreach (var type in entityTypes)
        {
            list.Add(BuildFull(type));
        }
        return list;
    }
}