using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Builders;

public partial class EntityDefinitionBuilder
{
    /// <summary>
    /// Sorts a list of <see cref="EntityDefinition"/> objects based on their foreign key dependencies.
    /// Ensures that referenced tables appear before dependent tables to avoid migration errors.
    /// </summary>
    /// <param name="entities">The list of entities to sort.</param>
    /// <returns>
    /// A new list of <see cref="EntityDefinition"/> objects sorted by dependency order.
    /// </returns>
    public static List<EntityDefinition> SortEntitiesByDependency(IEnumerable<EntityDefinition> entities)
    {
        var entityList = entities.ToList();
        var entityMap = entityList.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sorted = new List<EntityDefinition>();

        void Visit(EntityDefinition entity)
        {
            if (visited.Contains(entity.Name))
                return;

            visited.Add(entity.Name);

            foreach (var fk in entity.ForeignKeys)
            {
                if (entityMap.TryGetValue(fk.ReferencedTable, out var referencedEntity))
                {
                    Visit(referencedEntity);
                }
            }

            sorted.Add(entity);
        }

        foreach (var entity in entityList)
        {
            Visit(entity);
        }

        return sorted;
    }
    /// <summary>
    /// Infers foreign key relationships from navigation properties in the given CLR type.
    /// Looks for matching ID properties (e.g. Customer + CustomerId) and generates
    /// <see cref="ForeignKeyDefinition"/> entries if not already present.
    /// </summary>
    /// <param name="entityType">The CLR type representing the entity.</param>
    /// <param name="entity">The <see cref="EntityDefinition"/> being built.</param>
    private void InferForeignKeysFromNavigation(Type entityType, EntityDefinition entity)
    {
        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var navProp in props)
        {
            var navType = navProp.PropertyType;

            // Skip collections (handled separately)
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(navType) && navType != typeof(string))
                continue;

            // Skip primitives and strings
            if (!navType.IsClass || navType == typeof(string))
                continue;

            // Get referenced table info
            var (refSchema, refTable) = navType.GetTableInfo();

            // Look for matching FK property (e.g. CustomerId)
            var fkPropName = $"{navProp.Name}Id";
            var fkProp = props.FirstOrDefault(p => p.Name == fkPropName);
            if (fkProp == null)
                continue;

            var fkColumn = GetColumnName(fkProp);

            // Avoid duplicates
            if (entity.ForeignKeys.Any(fk => fk.Column == fkColumn))
                continue;

            // Add FK definition
            entity.ForeignKeys.Add(new ForeignKeyDefinition
            {
                Column = fkColumn,
                ReferencedTable = refTable,
                ConstraintName = $"FK_{entity.Name}_{fkColumn}"
            });
        }
    }

    /// <summary>
    /// Infers collection-based relationships (One-to-Many and Many-to-Many)
    /// from navigation properties of type ICollection&lt;T&gt;.
    /// Adds foreign keys to target entities and registers relationship metadata.
    /// </summary>
    /// <param name="entityType">The CLR type being analyzed.</param>
    /// <param name="entity">The <see cref="EntityDefinition"/> being built.</param>
    /// <param name="allEntities">All known entities for cross-reference and join table generation.</param>
    private void InferCollectionRelationships(Type entityType, EntityDefinition entity, List<EntityDefinition> allEntities)
    {
        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            var propType = prop.PropertyType;

            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(propType) || propType == typeof(string))
                continue;

            var itemType = propType.IsGenericType ? propType.GetGenericArguments().FirstOrDefault() : null;
            if (itemType == null || !itemType.IsClass || itemType == typeof(string))
                continue;

            var (targetSchema, targetTable) = itemType.GetTableInfo();
            var targetEntity = allEntities.FirstOrDefault(e => e.Name.Equals(targetTable, StringComparison.OrdinalIgnoreCase));
            if (targetEntity == null)
                continue;

            var expectedFkName = $"{entity.Name}Id";
            var hasFk = targetEntity.Columns.Any(c => c.Name.Equals(expectedFkName, StringComparison.OrdinalIgnoreCase));

            if (!hasFk)
            {
                targetEntity.Columns.Add(new ColumnDefinition
                {
                    Name = expectedFkName,
                    TypeName = "int",
                    IsNullable = false
                });

                targetEntity.ForeignKeys.Add(new ForeignKeyDefinition
                {
                    Column = expectedFkName,
                    ReferencedTable = entity.Name,
                    ConstraintName = $"FK_{targetEntity.Name}_{expectedFkName}"
                });
            }

            // سجل العلاقة One-to-Many
            entity.Relationships.Add(new RelationshipDefinition
            {
                SourceEntity = entity.Name,
                TargetEntity = targetEntity.Name,
                SourceProperty = prop.Name,
                Type = RelationshipType.OneToMany
            });

            // تحقق من وجود علاقة عكسية → Many-to-Many
            var reverseProps = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var reverseCollection = reverseProps.Any(p =>
                typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType) &&
                p.PropertyType.IsGenericType &&
                p.PropertyType.GetGenericArguments().FirstOrDefault() == entityType);

            if (reverseCollection)
            {
                var joinTableName = $"{entity.Name}_{targetEntity.Name}";

                var existingJoinEntity = allEntities.FirstOrDefault(e =>
                    e.Columns.Any(c => c.Name.Equals($"{entity.Name}Id", StringComparison.OrdinalIgnoreCase)) &&
                    e.Columns.Any(c => c.Name.Equals($"{targetEntity.Name}Id", StringComparison.OrdinalIgnoreCase)));

                var isExplicit = existingJoinEntity != null;

                entity.Relationships.Add(new RelationshipDefinition
                {
                    SourceEntity = entity.Name,
                    TargetEntity = targetEntity.Name,
                    SourceProperty = prop.Name,
                    Type = RelationshipType.ManyToMany,
                    JoinEntityName = joinTableName,
                    IsExplicitJoinEntity = isExplicit
                });

                if (!isExplicit)
                {
                    allEntities.Add(new EntityDefinition
                    {
                        Name = joinTableName,
                        Schema = entity.Schema,
                        ClrType = null,
                        Columns = new List<ColumnDefinition>
                    {
                        new ColumnDefinition { Name = $"{entity.Name}Id", TypeName = "int", IsNullable = false },
                        new ColumnDefinition { Name = $"{targetEntity.Name}Id", TypeName = "int", IsNullable = false }
                    },
                        ForeignKeys = new List<ForeignKeyDefinition>
                    {
                        new ForeignKeyDefinition
                        {
                            Column = $"{entity.Name}Id",
                            ReferencedTable = entity.Name,
                            ConstraintName = $"FK_{joinTableName}_{entity.Name}Id"
                        },
                        new ForeignKeyDefinition
                        {
                            Column = $"{targetEntity.Name}Id",
                            ReferencedTable = targetEntity.Name,
                            ConstraintName = $"FK_{joinTableName}_{targetEntity.Name}Id"
                        }
                    }
                    });
                }
            }
        }
    }

    /// <summary>
    /// Infers one-to-one relationships between entities based on navigation properties.
    /// Detects matching foreign keys and primary key alignment to confirm uniqueness.
    /// Registers relationship metadata in <see cref="RelationshipDefinition"/>.
    /// </summary>
    /// <param name="entityType">The CLR type being analyzed.</param>
    /// <param name="entity">The <see cref="EntityDefinition"/> being built.</param>
    /// <param name="allEntities">All known entities for cross-reference.</param>
    private void InferOneToOneRelationships(Type entityType, EntityDefinition entity, List<EntityDefinition> allEntities)
    {
        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var navProp in props)
        {
            var navType = navProp.PropertyType;

            // Skip primitives and collections
            if (!navType.IsClass || navType == typeof(string) ||
                typeof(System.Collections.IEnumerable).IsAssignableFrom(navType))
                continue;

            var (targetSchema, targetTable) = navType.GetTableInfo();
            var targetEntity = allEntities.FirstOrDefault(e => e.Name.Equals(targetTable, StringComparison.OrdinalIgnoreCase));
            if (targetEntity == null)
                continue;

            // Check if target entity has FK pointing back
            var expectedFkName = $"{entity.Name}Id";
            var fkColumn = targetEntity.Columns.FirstOrDefault(c => c.Name.Equals(expectedFkName, StringComparison.OrdinalIgnoreCase));
            var isFk = targetEntity.ForeignKeys.Any(fk => fk.Column.Equals(expectedFkName, StringComparison.OrdinalIgnoreCase));

            // Check if FK is also PK (i.e. unique relationship)
            var isPk = targetEntity.PrimaryKey?.Columns.Contains(expectedFkName) ?? false;

            if (fkColumn != null && isFk && isPk)
            {
                // ✅ سجل العلاقة One-to-One
                entity.Relationships.Add(new RelationshipDefinition
                {
                    SourceEntity = entity.Name,
                    TargetEntity = targetEntity.Name,
                    SourceProperty = navProp.Name,
                    Type = RelationshipType.OneToOne
                });
            }
        }
    }
}
