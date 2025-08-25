
using Syn.Core.SqlSchemaGenerator.AttributeHandlers;
using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Interfaces;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Syn.Core.SqlSchemaGenerator.Helper;

namespace Syn.Core.SqlSchemaGenerator.Builders;

/// <summary>
/// Provides functionality to build one or more <see cref="EntityDefinition"/> objects
/// from CLR types by applying schema attribute handlers.
/// </summary>
/// <remarks>
/// This builder can be initialized with a default set of handlers or with a custom collection.
/// Supports synchronous, asynchronous, and parallel building operations,
/// with overloads for scanning assemblies and applying filters.
/// </remarks>
/// <example>
/// <code>
/// // Example: Build entity definitions for all types in the current assembly
/// var builder = new EntityDefinitionBuilder();
/// var definitions = builder.Build(Assembly.GetExecutingAssembly());
/// </code>
/// </example>

public class EntityDefinitionBuilder
{
    private readonly IEnumerable<ISchemaAttributeHandler> _handlers;

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityDefinitionBuilder"/> class
    /// with a default set of schema attribute handlers.
    /// </summary>
    /// <remarks>
    /// Use this constructor when you want to start building entity definitions immediately
    /// without manually configuring handlers.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example: Using default handlers
    /// var builder = new EntityDefinitionBuilder();
    /// var definition = builder.Build(typeof(Customer));
    /// Console.WriteLine(definition.Name);
    /// </code>
    /// </example>
    public EntityDefinitionBuilder()
    {
        _handlers = new ISchemaAttributeHandler[]
        {
            new KeyAttributeHandler(),
            new IndexAttributeHandler(),
            new UniqueAttributeHandler(),
            new DefaultValueAttributeHandler(),
            new DescriptionAttributeHandler(),
            new RequiredAttributeHandler(),
            new MaxLengthAttributeHandler(),
            new ComputedAttributeHandler(),
            new CollationAttributeHandler(),
            new CheckConstraintAttributeHandler(),
            new IgnoreColumnAttributeHandler(),
            new EfCompatibilityAttributeHandler(),
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityDefinitionBuilder"/> class
    /// using the specified schema attribute handlers.
    /// </summary>
    /// <param name="handlers">The attribute handlers to apply to each property of an entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handlers"/> is null.</exception>
    /// <example>
    /// <code>
    /// // Example: Using custom handlers
    /// var handlers = new ISchemaAttributeHandler[]
    /// {
    ///     new IndexAttributeHandler(),
    ///     new DescriptionAttributeHandler()
    /// };
    /// var builder = new EntityDefinitionBuilder(handlers);
    /// var definition = builder.Build(typeof(Customer));
    /// </code>
    /// </example>
    public EntityDefinitionBuilder(IEnumerable<ISchemaAttributeHandler> handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    #endregion


    /// <summary>
    /// Builds an <see cref="EntityDefinition"/> instance from the specified CLR type.
    /// </summary>
    /// <param name="entityType">The CLR type representing the entity.</param>
    /// <returns>The constructed <see cref="EntityDefinition"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityType"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var builder = new EntityDefinitionBuilder();
    /// var def = builder.Build(typeof(Customer));
    /// Console.WriteLine($"Entity: {def.Name}, Columns: {def.Columns.Count}");
    /// </code>
    /// </example>
    public EntityDefinition Build(Type entityType)
    {
        var (schema, table) = entityType.GetTableInfo();

        var entity = new EntityDefinition
        {
            Name = table,
            Schema = schema
        };

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var columnName = GetColumnName(prop);
            var isNullable = IsNullable(prop);
            var maxLength = GetMaxLength(prop);
            var defaultValue = GetDefaultValue(prop);

            var columnModel = new ColumnModel
            {
                Name = columnName,
                PropertyType = prop.PropertyType,
                IsNullable = isNullable,
                MaxLength = maxLength,
                DefaultValue = defaultValue
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
            if (columnModel.ComputedExpression != null)
            {
                var computed = new ComputedColumnDefinition
                {
                    Name = columnName,
                    Expression = columnModel.ComputedExpression
                };
                entity.ComputedColumns.Add(computed);
            }

            // Check constraints
            var checks = ToCheckConstraints(columnModel, entity.Name);
            entity.CheckConstraints.AddRange(checks);

            // Indexes
            var indexes = ToIndexes(columnModel, entity.Name);
            entity.Indexes.AddRange(indexes);
        }

        entity.PrimaryKey = GetPrimaryKey(entityType);
        entity.UniqueConstraints = GetUniqueConstraints(entityType);
        entity.ForeignKeys = entityType.GetForeignKeys();
        ValidateForeignKeys(entity);

        var classLevelIndexes = GetIndexes(entityType);
        entity.Indexes.AddRange(classLevelIndexes);
        entity.Indexes = entity.Indexes
            .GroupBy(ix => ix.Name)
            .Select(g => g.First())
            .ToList();

        return entity;
    }

    /// <summary>
    /// Builds multiple <see cref="EntityDefinition"/> instances from the provided CLR types.
    /// </summary>
    /// <param name="entityTypes">The sequence of CLR types to process.</param>
    /// <returns>A list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityTypes"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var types = new[] { typeof(Customer), typeof(Order) };
    /// var defs = builder.Build(types);
    /// Console.WriteLine($"Definitions built: {defs.Count}");
    /// </code>
    /// </example>
    public IEnumerable<EntityDefinition> Build(IEnumerable<Type> entityTypes)
    {
        if (entityTypes == null)
            throw new ArgumentNullException(nameof(entityTypes));

        var result = new List<EntityDefinition>();
        foreach (var type in entityTypes)
        {
            result.Add(Build(type));
        }
        return result;
    }


    /// <summary>
    /// Builds multiple <see cref="EntityDefinition"/> instances by scanning all public
    /// CLR types in the specified assembly that implement <see cref="IDbEntity"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <returns>A list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assembly"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = builder.Build(Assembly.GetExecutingAssembly());
    /// foreach (var def in defs)
    /// {
    ///     Console.WriteLine($"{def.Name} => {def.Columns.Count} columns");
    /// }
    /// </code>
    /// </example>
    public IEnumerable<EntityDefinition> Build(Assembly assembly) =>
        Build(assembly, t => typeof(IDbEntity).IsAssignableFrom(t));


    /// <summary>
    /// Builds multiple <see cref="EntityDefinition"/> instances by scanning all public
    /// CLR types in the specified assembly that match the given filter predicate.
    /// </summary>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <param name="filter">
    /// Optional predicate to select which CLR types to include.
    /// </param>
    /// <returns>A list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assembly"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// // Example: Only include types ending with "Entity"
    /// var defs = builder.Build(
    ///     Assembly.GetExecutingAssembly(),
    ///     t => t.Name.EndsWith("Entity")
    /// );
    /// </code>
    /// </example>
    public IEnumerable<EntityDefinition> Build(Assembly assembly, Func<Type, bool>? filter = null)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly
            .GetTypes()
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
            .Where(t => filter == null || filter(t)).ToList();

        return Build(entityTypes);
    }

    #region Asynchronous Methods

    /// <summary>
    /// Asynchronously builds an <see cref="EntityDefinition"/> instance from the specified CLR type.
    /// </summary>
    /// <param name="entityType">The CLR type representing the entity.</param>
    /// <returns>A task that represents the asynchronous operation, containing the constructed <see cref="EntityDefinition"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityType"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var def = await builder.BuildAsync(typeof(Customer));
    /// Console.WriteLine($"Entity: {def.Name}");
    /// </code>
    /// </example>
    public async Task<EntityDefinition> BuildAsync(Type entityType)
    {
        return await Task.Run(() => Build(entityType));
    }

    /// <summary>
    /// Asynchronously builds multiple <see cref="EntityDefinition"/> instances from the specified CLR types.
    /// </summary>
    /// <param name="entityTypes">The sequence of CLR types to process.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityTypes"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = await builder.BuildAsync(new[] { typeof(Customer), typeof(Order) });
    /// Console.WriteLine($"Built {defs.Count} definitions");
    /// </code>
    /// </example>
    public async Task<List<EntityDefinition>> BuildAsync(IEnumerable<Type> entityTypes)
    {
        return await Task.Run(() => Build(entityTypes).ToList());
    }

    /// <summary>
    /// Asynchronously builds multiple <see cref="EntityDefinition"/> instances by scanning all public
    /// CLR types in the specified assembly that implement <see cref="IDbEntity"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assembly"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = await builder.BuildAsync(Assembly.GetExecutingAssembly());
    /// foreach (var def in defs)
    /// {
    ///     Console.WriteLine($"{def.Name} => {def.Columns.Count} columns");
    /// }
    /// </code>
    /// </example>
    public async Task<List<EntityDefinition>> BuildAsync(Assembly assembly)
    {
        return await Task.Run(() => Build(assembly).ToList());
    }

    /// <summary>
    /// Asynchronously builds multiple <see cref="EntityDefinition"/> instances by scanning all public
    /// CLR types in the specified assembly that match the given filter predicate.
    /// </summary>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <param name="filter">Optional predicate to select which CLR types to include.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assembly"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = await builder.BuildAsync(
    ///     Assembly.GetExecutingAssembly(),
    ///     t => t.Name.Contains("Audit")
    /// );
    /// Console.WriteLine($"Filtered entity count: {defs.Count}");
    /// </code>
    /// </example>
    public async Task<List<EntityDefinition>> BuildAsync(Assembly assembly, Func<Type, bool>? filter = null)
    {
        return await Task.Run(() => Build(assembly, filter).ToList());
    }


    #region Parallel Async Methods

    /// <summary>
    /// Builds multiple <see cref="EntityDefinition"/> instances in parallel from the specified CLR types.
    /// </summary>
    /// <param name="entityTypes">The sequence of CLR types to process.</param>
    /// <returns>A task representing the asynchronous parallel operation, containing a list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityTypes"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = await builder.BuildParallelAsync(new[] { typeof(Customer), typeof(Order) });
    /// Console.WriteLine($"Parallel build completed: {defs.Count} definitions");
    /// </code>
    /// </example>
    public async Task<List<EntityDefinition>> BuildParallelAsync(IEnumerable<Type> entityTypes)
    {
        if (entityTypes == null)
            throw new ArgumentNullException(nameof(entityTypes));

        var results = new ConcurrentBag<EntityDefinition>();

        await Parallel.ForEachAsync(entityTypes, async (type, ct) =>
        {
            // هنا ممكن نعمل await لعمليات I/O مستقبلية لو موجودة
            var entityDef = Build(type);
            results.Add(entityDef);
            await Task.CompletedTask;
        });

        return results.ToList();
    }

    /// <summary>
    /// Builds multiple <see cref="EntityDefinition"/> instances in parallel by scanning all public
    /// CLR types in the specified assembly that implement <see cref="IDbEntity"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <returns>A task representing the asynchronous parallel operation, containing a list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assembly"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = await builder.BuildParallelAsync(Assembly.GetExecutingAssembly());
    /// Console.WriteLine($"Entities built in parallel: {defs.Count}");
    /// </code>
    /// </example>
    public async Task<List<EntityDefinition>> BuildParallelAsync(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly
            .DefinedTypes
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract && typeof(IDbEntity).IsAssignableFrom(t))
            .Select(t => t.AsType())
            .ToList();

        return await BuildParallelAsync(entityTypes);
    }

    /// <summary>
    /// Builds multiple <see cref="EntityDefinition"/> instances in parallel by scanning all public
    /// CLR types in the specified assembly that match a filter predicate.
    /// </summary>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <param name="filter">Optional predicate to select which CLR types to include.</param>
    /// <returns>A task representing the asynchronous parallel operation, containing a list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assembly"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = await builder.BuildParallelAsync(Assembly.GetExecutingAssembly(), t => t.Name.EndsWith("Entity"));
    /// </code>
    /// </example>
    public async Task<List<EntityDefinition>> BuildParallelAsync(Assembly assembly, Func<Type, bool>? filter = null)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly
            .DefinedTypes
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
            .Where(t => filter == null || filter(t.AsType()))
            .Select(t => t.AsType())
            .ToList();

        return await BuildParallelAsync(entityTypes);
    }

    /// <summary>
    /// Builds multiple <see cref="EntityDefinition"/> instances in parallel with configurable
    /// maximum degree of parallelism and optional schema override.
    /// </summary>
    /// <param name="entityTypes">The sequence of CLR types to process.</param>
    /// <param name="maxDegreeOfParallelism">Optional maximum number of concurrent tasks.</param>
    /// <param name="schema">Optional schema name to override default entity schemas.</param>
    /// <returns>A task representing the asynchronous parallel operation, containing a list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityTypes"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = await builder.BuildParallelAsync(
    ///     new[] { typeof(Customer), typeof(Order) },
    ///     maxDegreeOfParallelism: 4,
    ///     schema: "audit"
    /// );
    /// </code>
    /// </example>
    public async Task<List<EntityDefinition>> BuildParallelAsync(
        IEnumerable<Type> entityTypes,
        int? maxDegreeOfParallelism = null,
        string? schema = null)
    {
        if (entityTypes == null)
            throw new ArgumentNullException(nameof(entityTypes));

        var results = new ConcurrentBag<EntityDefinition>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount
        };

        await Parallel.ForEachAsync(entityTypes, parallelOptions, async (type, ct) =>
        {
            var entityDef = Build(type);
            if (!string.IsNullOrWhiteSpace(schema))
                entityDef.Schema = schema;

            results.Add(entityDef);
            await Task.CompletedTask;
        });

        return results.ToList();
    }

    /// <summary>
    /// Builds multiple <see cref="EntityDefinition"/> instances in parallel by scanning all public
    /// CLR types in the specified assembly that match a filter predicate, with configurable
    /// maximum degree of parallelism and optional schema override.
    /// </summary>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <param name="filter">Optional predicate to select which CLR types to include.</param>
    /// <param name="maxDegreeOfParallelism">Optional maximum number of concurrent tasks.</param>
    /// <param name="schema">Optional schema name to override default entity schemas.</param>
    /// <returns>A task representing the asynchronous parallel operation, containing a list of constructed <see cref="EntityDefinition"/> objects.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assembly"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var defs = await builder.BuildParallelAsync(
    ///     Assembly.GetExecutingAssembly(),
    ///     t => t.Name.StartsWith("Tbl"),
    ///     maxDegreeOfParallelism: 2,
    ///     schema: "archive"
    /// );
    /// </code>
    /// </example>
    public async Task<List<EntityDefinition>> BuildParallelAsync(
        Assembly assembly,
        Func<Type, bool>? filter = null,
        int? maxDegreeOfParallelism = null,
        string? schema = null)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly
            .DefinedTypes
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
            .Where(t => filter == null || filter(t.AsType()))
            .Select(t => t.AsType())
            .ToList();

        return await BuildParallelAsync(entityTypes, maxDegreeOfParallelism, schema);
    }
    #endregion

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
            IsForeignKey = model.IsForeignKey,
            IsUnique = model.IsUnique,
            IsPrimaryKey = model.IsPrimaryKey,
            UniqueConstraintName = model.UniqueConstraintName,
            IgnoreReason = model.IgnoreReason,
            IsIgnored = model.IsIgnored,
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

    private static List<IndexDefinition> GetIndexes(Type type)
    {
        var indexes = new List<IndexDefinition>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var indexAttrs = prop.GetCustomAttributes<IndexAttribute>();
            foreach (var attr in indexAttrs)
            {
                indexes.Add(new IndexDefinition
                {
                    Name = attr.Name ?? $"IX_{type.Name}_{prop.Name}",
                    Columns = new List<string> { GetColumnName(prop) },
                    IsUnique = attr.IsUnique
                });
            }
        }

        return indexes;
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
    /// Extracts primary key columns from [Key] attributes.
    /// </summary>
    private static PrimaryKeyDefinition? GetPrimaryKey(Type type)
    {
        var pkColumns = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<KeyAttribute>() != null)
            .Select(p => p.Name)
            .ToList();

        if (pkColumns.Count == 0)
            return null;

        return new PrimaryKeyDefinition
        {
            Columns = pkColumns,
            IsAutoGenerated = true
        };
    }


    /// <summary>
    /// Extracts unique constraints from [Unique] and [Index(IsUnique = true)] attributes.
    /// Supports both property-level and class-level Index definitions.
    /// </summary>
    /// <param name="type">The entity type to inspect.</param>
    /// <returns>List of unique constraint definitions.</returns>
    private static List<UniqueConstraintDefinition> GetUniqueConstraints(Type type)
    {
        var uniqueConstraints = new List<UniqueConstraintDefinition>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ✅ Property-level: [Unique] or [Index(IsUnique = true)]
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var columnName = prop.Name;

            // [Unique] (custom)
            if (prop.GetCustomAttribute<UniqueAttribute>() != null)
            {
                var key = $"UQ_{type.Name}_{columnName}";
                if (seenKeys.Add(key))
                {
                    uniqueConstraints.Add(new UniqueConstraintDefinition
                    {
                        Name = key,
                        Columns = new List<string> { columnName },
                        IsAutoGenerated = true
                    });
                }
            }

            // [Index(IsUnique = true)] from EF Core
            var indexAttrs = prop.GetCustomAttributes<IndexAttribute>();
            foreach (var indexAttr in indexAttrs)
            {
                if (indexAttr.IsUnique)
                {
                    var name = indexAttr.Name ?? $"UQ_{type.Name}_{columnName}";
                    if (seenKeys.Add(name))
                    {
                        uniqueConstraints.Add(new UniqueConstraintDefinition
                        {
                            Name = name,
                            Columns = new List<string> { columnName },
                            IsAutoGenerated = true
                        });
                    }
                }
            }
        }

        // ✅ Class-level: [Index(nameof(Column1), nameof(Column2), IsUnique = true)]
        var classLevelIndexes = type.GetCustomAttributes<IndexAttribute>();
        foreach (var indexAttr in classLevelIndexes)
        {
            if (indexAttr.IsUnique && indexAttr.Columns?.Length > 0)
            {
                var name = indexAttr.Name ?? $"UQ_{type.Name}_{string.Join("_", indexAttr.Columns)}";
                if (seenKeys.Add(name))
                {
                    uniqueConstraints.Add(new UniqueConstraintDefinition
                    {
                        Name = name,
                        Columns = indexAttr.Columns?.ToList(),
                        IsAutoGenerated = true
                    });
                }
            }
        }

        return uniqueConstraints;
    }


    

    private static void ValidateForeignKeys(EntityDefinition entity)
    {
        foreach (var fk in entity.ForeignKeys)
        {
            if (!entity.Columns.Any(c => c.Name == fk.Column))
            {
                throw new InvalidOperationException(
                    $"Foreign key column '{fk.Column}' not found in entity '{entity.Name}'."
                );
            }
        }
    }


    /// <summary>
    /// Determines nullability of columns based on [Required] attribute.
    /// </summary>
    private static bool IsNullable(PropertyInfo prop)
    {
        return prop.GetCustomAttribute<RequiredAttribute>() == null;
    }

    /// <summary>
    /// Extracts max length constraint from [MaxLength] attribute.
    /// </summary>
    private static int? GetMaxLength(PropertyInfo prop)
    {
        var maxAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
        return maxAttr?.Length;
    }

    /// <summary>
    /// Extracts column name override from [Column] attribute.
    /// </summary>
    private static string GetColumnName(PropertyInfo prop)
    {
        var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
        return colAttr?.Name?.Trim() ?? prop.Name;
    }

    /// <summary>
    /// Extracts default value from [DefaultValue] attribute.
    /// </summary>
    private static object? GetDefaultValue(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<DefaultValueAttribute>();
        return attr?.Value;
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
#endregion