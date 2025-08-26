using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System.ComponentModel.DataAnnotations;
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
    /// <summary>
    /// Infers One-to-One relationships based on navigation properties and FK/PK structure.
    /// Adds relationship metadata to both source and target entities to support constraint generation.
    /// </summary>
    /// <summary>
    /// Infers One-to-One relationships between entities based on navigation properties and FK/PK structure.
    /// Automatically sets CascadeDelete when the relationship pattern matches common ownership (e.g. UserProfile → User).
    /// </summary>
    /// <param name="entityType">The CLR type of the source entity.</param>
    /// <param name="entity">The EntityDefinition representing the source entity.</param>
    /// <param name="allEntities">All known entities for cross-reference.</param>
    /// <summary>
    /// Infers One-to-One relationships based on foreign keys that are also unique or primary keys.
    /// Adds the relationship to both the source and target entities.
    /// </summary>

    /// <summary>
    /// Infers One-to-One relationships:
    /// - يعتبر العلاقة One-to-One لو الـ FK عليه Unique أو PK
    /// - أو لو فيه ملاحة مفردة متبادلة (Reference ↔ Reference)
    /// - يمنع إضافة One-to-One لو فيه علاقة 1:N أو N:N بين نفس الكيانين
    /// </summary>
    public void InferOneToOneRelationships(Type clrType, EntityDefinition entity, List<EntityDefinition> allEntities)
    {
        Console.WriteLine($"[TRACE:OneToOne] Analyzing entity {entity.Name}");
        if (entity.ForeignKeys == null || entity.ForeignKeys.Count == 0)
        {
            Console.WriteLine("  No foreign keys found.");
            return;
        }

        foreach (var fk in entity.ForeignKeys)
        {
            Console.WriteLine($"  FK found: {fk.Column} -> {fk.ReferencedTable}");

            var targetEntity = allEntities.FirstOrDefault(e =>
                e.Name.Equals(fk.ReferencedTable, StringComparison.OrdinalIgnoreCase));
            if (targetEntity == null)
            {
                Console.WriteLine("    Target entity not found in allEntities.");
                continue;
            }

            // ✅ لا نضيف 1:1 لو فيه 1:N أو N:N بين نفس الزوج
            bool alreadyHasNonOneToOne =
                entity.Relationships.Any(r => r.TargetEntity == targetEntity.Name && r.Type != RelationshipType.OneToOne) ||
                targetEntity.Relationships.Any(r => r.TargetEntity == entity.Name && r.Type != RelationshipType.OneToOne);

            if (alreadyHasNonOneToOne)
            {
                Console.WriteLine($"    Skipped 1:1: non-OneToOne relationship already exists between {entity.Name} and {targetEntity.Name}");
                continue;
            }

            bool isUnique = entity.UniqueConstraints.Any(u =>
                u.Columns.Count == 1 &&
                u.Columns.Contains(fk.Column, StringComparer.OrdinalIgnoreCase));

            bool isAlsoPrimaryKey = entity.PrimaryKey?.Columns?.Count == 1 &&
                                    entity.PrimaryKey.Columns.Contains(fk.Column, StringComparer.OrdinalIgnoreCase);

            bool hasRefToTarget = HasSingleReferenceNavigation(clrType, targetEntity.ClrType);
            bool targetHasRefBack = HasSingleReferenceNavigation(targetEntity.ClrType, clrType);

            bool isStrictOneToOne = isUnique || isAlsoPrimaryKey;
            bool isNavOneToOne = hasRefToTarget && targetHasRefBack;

            if (!isStrictOneToOne && !isNavOneToOne)
            {
                Console.WriteLine("    Skipped 1:1: neither unique/PK nor mutual single navigations.");
                continue;
            }

            if (!isStrictOneToOne && isNavOneToOne)
            {
                var uqName = $"UQ_{entity.Name}_{fk.Column}";
                if (!entity.UniqueConstraints.Any(u => u.Name.Equals(uqName, StringComparison.OrdinalIgnoreCase)))
                {
                    entity.UniqueConstraints.Add(new UniqueConstraintDefinition
                    {
                        Name = uqName,
                        Columns = new List<string> { fk.Column },
                        Description = $"Auto-unique for 1:1 {entity.Name} → {targetEntity.Name}"
                    });
                    Console.WriteLine($"    ✅ Auto-added UNIQUE: {uqName}");
                }
            }

            entity.Relationships.Add(new RelationshipDefinition
            {
                SourceEntity = entity.Name,
                TargetEntity = targetEntity.Name,
                Type = RelationshipType.OneToOne,
                OnDelete = fk.OnDelete == ReferentialAction.Cascade ? ReferentialAction.Cascade : ReferentialAction.NoAction
            });

            targetEntity.Relationships.Add(new RelationshipDefinition
            {
                SourceEntity = targetEntity.Name,
                TargetEntity = entity.Name,
                Type = RelationshipType.OneToOne,
                OnDelete = fk.OnDelete == ReferentialAction.Cascade ? ReferentialAction.Cascade : ReferentialAction.NoAction
            });

            Console.WriteLine($"    ✅ OneToOne relationship added: {entity.Name} <-> {targetEntity.Name}");
        }
    }

    /// <summary>
    /// Detects if the type has a single reference navigation to another type (non-collection, non-string).
    /// </summary>
    private static bool HasSingleReferenceNavigation(Type from, Type to)
    {
        return from.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                  .Any(p => p.PropertyType == to);
    }
    /// <summary>
    /// Infers CHECK constraints from validation attributes more broadly:
    /// - [StringLength], [MaxLength], [MinLength]
    /// - [Range] for numeric types
    /// - [Required] for any type (string/non-string)
    /// </summary>
    public void InferCheckConstraints(Type clrType, EntityDefinition entity)
    {
        Console.WriteLine($"[TRACE:CheckConstraints] Analyzing entity {entity.Name}");

        // 🥇 PK: Not Null + Identity
        if (entity.PrimaryKey?.Columns != null)
        {
            foreach (var pkColName in entity.PrimaryKey.Columns)
            {
                var col = entity.Columns.FirstOrDefault(c =>
                    c.Name.Equals(pkColName, StringComparison.OrdinalIgnoreCase));
                if (col != null)
                {
                    col.IsNullable = false;
                    col.IsIdentity = true; // بدون Seed/Increment
                    Console.WriteLine($"    ✅ PK {col.Name}: Not Null + Identity");
                }
            }
        }

        // 🥈 تحليل كل الأعمدة
        foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var colName = GetColumnName(prop);
            var col = entity.Columns.FirstOrDefault(c =>
                c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
            if (col == null) continue;

            // 1) قيد أساسي من Nullability + نوع العمود
            if (!col.IsNullable)
            {
                if (IsStringColumn(col))
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = $"CK_{entity.Name}_{col.Name}_NotEmpty",
                        Expression = $"LEN([{col.Name}]) > 0",
                        Description = $"{col.Name} must not be empty"
                    });
                    Console.WriteLine($"    ✅ Added CHECK (NotEmpty) on {col.Name}");
                }
                else
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = $"CK_{entity.Name}_{col.Name}_NotNull",
                        Expression = $"[{col.Name}] IS NOT NULL",
                        Description = $"{col.Name} must not be NULL"
                    });
                    Console.WriteLine($"    ✅ Added CHECK (NotNull) on {col.Name}");
                }
            }

            // 2) من Attributes
            var strLenAttr = prop.GetCustomAttribute<StringLengthAttribute>();
            if (strLenAttr?.MaximumLength > 0)
            {
                entity.CheckConstraints.Add(new CheckConstraintDefinition
                {
                    Name = $"CK_{entity.Name}_{col.Name}_MaxLen",
                    Expression = $"LEN([{col.Name}]) <= {strLenAttr.MaximumLength}",
                    Description = $"Max length of {col.Name} is {strLenAttr.MaximumLength} characters"
                });
                Console.WriteLine($"    ✅ Added CHECK (StringLength) on {col.Name}");
            }

            var maxLenAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
            if (maxLenAttr?.Length > 0)
            {
                entity.CheckConstraints.Add(new CheckConstraintDefinition
                {
                    Name = $"CK_{entity.Name}_{col.Name}_MaxLen",
                    Expression = $"LEN([{col.Name}]) <= {maxLenAttr.Length}",
                    Description = $"Max length of {col.Name} is {maxLenAttr.Length} characters"
                });
                Console.WriteLine($"    ✅ Added CHECK (MaxLength) on {col.Name}");
            }

            var minLenAttr = prop.GetCustomAttribute<MinLengthAttribute>();
            if (minLenAttr?.Length > 0)
            {
                entity.CheckConstraints.Add(new CheckConstraintDefinition
                {
                    Name = $"CK_{entity.Name}_{col.Name}_MinLen",
                    Expression = $"LEN([{col.Name}]) >= {minLenAttr.Length}",
                    Description = $"Min length of {col.Name} is {minLenAttr.Length} characters"
                });
                Console.WriteLine($"    ✅ Added CHECK (MinLength) on {col.Name}");
            }

            var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr?.Minimum != null && rangeAttr.Maximum != null)
            {
                entity.CheckConstraints.Add(new CheckConstraintDefinition
                {
                    Name = $"CK_{entity.Name}_{col.Name}_Range",
                    Expression = $"[{col.Name}] BETWEEN {rangeAttr.Minimum} AND {rangeAttr.Maximum}",
                    Description = $"{col.Name} must be between {rangeAttr.Minimum} and {rangeAttr.Maximum}"
                });
                Console.WriteLine($"    ✅ Added CHECK (Range) on {col.Name}");
            }

            var requiredAttr = prop.GetCustomAttribute<RequiredAttribute>();
            if (requiredAttr != null)
            {
                var expr = IsStringColumn(col)
                    ? $"LEN([{col.Name}]) > 0"
                    : $"[{col.Name}] IS NOT NULL";
                entity.CheckConstraints.Add(new CheckConstraintDefinition
                {
                    Name = $"CK_{entity.Name}_{col.Name}_Required",
                    Expression = expr,
                    Description = $"{col.Name} is required"
                });
                Console.WriteLine($"    ✅ Added CHECK (Required) on {col.Name}");
            }
        }
    }

    /// <summary>
    /// Detects string SQL types, covering sizes and (max).
    /// </summary>
    private bool IsStringColumn(ColumnDefinition col)
    {
        if (string.IsNullOrEmpty(col.TypeName)) return false;
        var t = col.TypeName.Split('(')[0].Trim().ToLowerInvariant();
        return t == "nvarchar" || t == "varchar" ||
               t == "char" || t == "nchar" ||
               t == "text" || t == "ntext";
    }
    ///// <summary>
    ///// Helper method to check if the SQL column type is a string type (covers sizes and max).
    ///// </summary>
    //private bool IsStringColumn(ColumnDefinition col)
    //{
    //    var t = col.TypeName?.Trim().ToLowerInvariant();
    //    return t.StartsWith("nvarchar") ||
    //           t.StartsWith("varchar") ||
    //           t.StartsWith("char") ||
    //           t.StartsWith("nchar") ||
    //           t.StartsWith("text") ||
    //           t.StartsWith("ntext");
    //}

}
