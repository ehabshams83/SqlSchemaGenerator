
using Syn.Core.SqlSchemaGenerator.AttributeHandlers;
using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Interfaces;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

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

public partial class EntityDefinitionBuilder
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
        _handlers = _attributeseHandlers;
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
    public EntityDefinitionBuilder(IEnumerable<ISchemaAttributeHandler> handlers = null)
    {
        _handlers = handlers ?? _attributeseHandlers;
    }

    #endregion



    /// <summary>
    /// Builds an <see cref="EntityDefinition"/> model from the specified entity <see cref="Type"/>.
    /// This method scans the entity type's public instance properties, applies column handlers,
    /// and populates all relevant metadata, including columns, computed columns, constraints,
    /// indexes, and optional descriptions. The resulting definition is ready for SQL generation
    /// using the unified <c>Build(EntityDefinition)</c> method.
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

    /// <returns>A fully populated <see cref="EntityDefinition"/> instance.</returns>
    public EntityDefinition Build(Type entityType)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));

        var (schema, table) = entityType.GetTableInfo();

        var entity = new EntityDefinition
        {
            Name = table,
            Schema = schema,
            ClrType = entityType
        };

        // وصف الجدول
        var tableDescAttr = entityType.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        if (!string.IsNullOrWhiteSpace(tableDescAttr?.Description))
            entity.Description = tableDescAttr.Description;

        // الأعمدة (بدون Navigation Properties)
        foreach (var prop in entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null && !IsNavigationProperty(p)))
        {
            BuildColumn(prop, entity);
        }

        // المفتاح الأساسي
        entity.PrimaryKey = GetPrimaryKey(entityType);
        if (entity.PrimaryKey != null && string.IsNullOrWhiteSpace(entity.PrimaryKey.Name))
            entity.PrimaryKey.Name = $"PK_{entity.Name}";

        ApplyPrimaryKeyOverrides(entity);

        // المفاتيح الأجنبية (بدون تحليل علاقات)
        entity.ForeignKeys = BuildForeignKeys(entityType, entity.Name);

        // استنتاج المفاتيح الأجنبية من الـ Navigation (لكن بدون OneToOne هنا)
        InferForeignKeysFromNavigation(entityType, entity);

        // ✅ لا يوجد InferOneToOneRelationships هنا — هيتعمل في Pass 2

        ValidateForeignKeys(entity);

        // الفهارس من EF
        entity.Indexes.AddRange(GetIndexes(entityType));

        // فهارس CHECK
        foreach (var ck in entity.CheckConstraints)
        {
            foreach (var colName in ck.ReferencedColumns)
            {
                bool alreadyIndexed = entity.Indexes.Any(ix => ix.Columns.Contains(colName, StringComparer.OrdinalIgnoreCase));
                if (!alreadyIndexed && entity.Columns.Any(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase)))
                {
                    entity.Indexes.Add(new IndexDefinition
                    {
                        Name = $"IX_{entity.Name}_{colName}_ForCheck",
                        Columns = new List<string> { colName },
                        IsUnique = false,
                        Description = $"Auto index to support CHECK constraint {ck.Name}"
                    });
                }
            }
        }

        // فهارس الأعمدة الحساسة
        var sensitiveNames = new[] { "Email", "Username", "Code" };
        foreach (var col in entity.Columns)
        {
            if (sensitiveNames.Contains(col.Name, StringComparer.OrdinalIgnoreCase))
            {
                var alreadyIndexed = entity.Indexes.Any(ix => ix.Columns.Contains(col.Name, StringComparer.OrdinalIgnoreCase));
                if (!alreadyIndexed)
                {
                    entity.Indexes.Add(new IndexDefinition
                    {
                        Name = $"IX_{entity.Name}_{col.Name}_AutoSensitive",
                        Columns = new List<string> { col.Name },
                        IsUnique = true,
                        Description = "Auto-generated index for login-critical field"
                    });
                }
            }
        }

        // فهارس أعمدة العلاقات (لو فيه علاقات اتبنت لاحقًا)
        foreach (var rel in entity.Relationships)
        {
            var fkColumn = rel.SourceToTargetColumn ?? $"{rel.TargetEntity}Id";
            var alreadyIndexed = entity.Indexes.Any(ix => ix.Columns.Contains(fkColumn, StringComparer.OrdinalIgnoreCase));
            if (!alreadyIndexed && entity.Columns.Any(c => c.Name.Equals(fkColumn, StringComparison.OrdinalIgnoreCase)))
            {
                entity.Indexes.Add(new IndexDefinition
                {
                    Name = $"IX_{entity.Name}_{fkColumn}_AutoNav",
                    Columns = new List<string> { fkColumn },
                    IsUnique = false,
                    Description = "Auto-generated index for navigation property"
                });
            }
        }

        // إزالة التكرار في الفهارس
        entity.Indexes = entity.Indexes
            .GroupBy(ix => ix.Name)
            .Select(g => g.First())
            .ToList();

        // استنتاج قيود CHECK بعد تثبيت الأسماء
        InferCheckConstraints(entityType, entity);

        return entity;
    }
    private bool IsCollectionOfEntity(PropertyInfo prop)
    {
        if (prop.PropertyType == typeof(string)) return false;
        if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType)) return false;
        if (!prop.PropertyType.IsGenericType) return false;
        var arg = prop.PropertyType.GetGenericArguments()[0];
        return arg.IsClass && !arg.Namespace.StartsWith("System", StringComparison.Ordinal);
    }

    private bool IsReferenceToEntity(PropertyInfo prop)
    {
        var t = prop.PropertyType;
        return t != typeof(string) && t.IsClass && !t.Namespace.StartsWith("System", StringComparison.Ordinal);
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

        return BuildAllWithRelationships(entityTypes);
    }

    /// <summary>
    /// Builds all entity definitions from the provided CLR types and enriches them with inferred relationships and constraints.
    /// Includes foreign keys, collection relationships, one-to-one relationships, and check constraints.
    /// </summary>
    /// <param name="entityTypes">The CLR types representing entities.</param>
    /// <returns>A list of enriched EntityDefinition objects.</returns>
    /// <summary>
    /// Builds all entity definitions from the provided CLR types and enriches them
    /// with inferred relationships and constraints in two passes to ensure complete metadata.
    /// </summary>
    /// <param name="entityTypes">The CLR types representing entities.</param>
    /// <returns>A list of enriched EntityDefinition objects.</returns>
    /// 

    public List<EntityDefinition> BuildAllWithRelationships(IEnumerable<Type> entityTypes)
    {
        if (entityTypes == null)
            throw new ArgumentNullException(nameof(entityTypes));

        // ===== Pass 1: بناء الكيانات الأساسية =====
        Console.WriteLine("===== [TRACE] Pass 1: Building basic entities =====");

        var allEntities = entityTypes
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
            .Select(t =>
            {
                Console.WriteLine($"[TRACE:Build] Including type: {t.Name}");
                var entity = Build(t); // يبني الأعمدة، الـ PK، والـ Checks المحلية فقط
                entity.ClrType = t;
                return entity;
            })
            .ToList();

        // ===== Pass 2: تحليل العلاقات =====
        Console.WriteLine("===== [TRACE] Pass 2: Inferring relationships and constraints =====");

        foreach (var entity in allEntities)
        {
            InferOneToOneRelationships(entity.ClrType, entity, allEntities);
        }

        foreach (var entity in allEntities)
        {
            InferCollectionRelationships(entity.ClrType, entity, allEntities);
        }
        Console.WriteLine();
        // ===== Pass 3: خطوات إضافية (اختياري) =====
        Console.WriteLine("===== [TRACE] Pass 3: Finalizing =====");
        foreach (var entity in allEntities)
        {
            entity.Indexes.AddRange(AddCheckConstraintIndexes(entity));
            entity.Indexes.AddRange(AddSensitiveIndexes(entity));
            entity.Indexes.AddRange(AddNavigationIndexes(entity));

            entity.Indexes = entity.Indexes
                .GroupBy(ix => ix.Name)
                .Select(g => g.First())
                .ToList();

            InferCheckConstraints(entity.ClrType, entity); // بعد الفهارس
        }

        return allEntities;
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
    /// Converts a ColumnModel to a ColumnDefinition, copying all relevant properties.
    /// </summary>
    private static ColumnDefinition ToColumnDefinition(ColumnModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        return model.MapTo<ColumnModel, ColumnDefinition>();
        //return new ColumnDefinition
        //{
        //    // 🔹 Core Properties
        //    Name = model.Name,
        //    TypeName = model.TypeName ?? model.PropertyType.MapClrTypeToSql(),
        //    Precision = model.Precision,
        //    Scale = model.Scale,
        //    IsNullable = model.IsNullable,
        //    DefaultValue = model.DefaultValue,
        //    Collation = model.Collation,
        //    ComputedExpression = model.ComputedExpression,
        //    Order = model.Order,
        //    Comment = model.Comment,
        //    Description = model.Description,

        //    // 🔹 Constraints & Identity
        //    IsIdentity = model.IsIdentity,
        //    IsPrimaryKey = model.IsPrimaryKey,
        //    IsUnique = model.IsUnique,
        //    UniqueConstraintName = model.UniqueConstraintName,
        //    CheckConstraints = model.CheckConstraints != null
        //        ? new List<CheckConstraintDefinition>(model.CheckConstraints)
        //        : new List<CheckConstraintDefinition>(),

        //    // 🔹 Indexing
        //    Indexes = model.Indexes != null
        //        ? new List<IndexDefinition>(model.Indexes)
        //        : new List<IndexDefinition>(),

        //    // 🔹 Foreign Key & Navigation
        //    IsForeignKey = model.IsForeignKey,
        //    ForeignKeyTarget = model.ForeignKeyTarget,
        //    IsNavigationProperty = model.IsNavigationProperty,

        //    // 🔹 Metadata & Control
        //    IsIgnored = model.IsIgnored,
        //    IgnoreReason = model.IgnoreReason,

        //    // 🆕 Persisted flag for computed columns
        //    IsPersisted = model.IsPersisted
        //};
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
    public static List<CheckConstraintDefinition> ToCheckConstraints(PropertyInfo prop, string entityName)
    {
        var result = new List<CheckConstraintDefinition>();
        var colName = GetColumnName(prop);
        var referenced = new List<string> { colName };

        bool isString = prop.PropertyType == typeof(string) || prop.PropertyType == typeof(char);

        // 🔹 دالة لتوحيد وتطبيع الاسم
        string Normalize(string s) => s?.Trim().Replace(" ", "_");

        // ✅ Required
        if (prop.GetCustomAttribute<RequiredAttribute>() != null)
        {
            var expr = isString ? $"LEN([{colName}]) > 0" : $"[{colName}] IS NOT NULL";
            result.Add(new CheckConstraintDefinition
            {
                Name = $"CK_{Normalize(entityName)}_{Normalize(colName)}_Required",
                Expression = expr,
                Description = $"{colName} is required",
                ReferencedColumns = referenced
            });
        }

        // ✅ StringLength
        var strLen = prop.GetCustomAttribute<StringLengthAttribute>();
        if (strLen?.MaximumLength > 0)
        {
            result.Add(new CheckConstraintDefinition
            {
                Name = $"CK_{Normalize(entityName)}_{Normalize(colName)}_MaxLen",
                Expression = $"LEN([{colName}]) <= {strLen.MaximumLength}",
                Description = $"Max length of {colName} is {strLen.MaximumLength} characters",
                ReferencedColumns = referenced
            });
        }

        // ✅ MaxLength
        var maxLen = prop.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLen?.Length > 0)
        {
            result.Add(new CheckConstraintDefinition
            {
                Name = $"CK_{Normalize(entityName)}_{Normalize(colName)}_MaxLen",
                Expression = $"LEN([{colName}]) <= {maxLen.Length}",
                Description = $"Max length of {colName} is {maxLen.Length} characters",
                ReferencedColumns = referenced
            });
        }

        // ✅ MinLength
        var minLen = prop.GetCustomAttribute<MinLengthAttribute>();
        if (minLen?.Length > 0)
        {
            result.Add(new CheckConstraintDefinition
            {
                Name = $"CK_{Normalize(entityName)}_{Normalize(colName)}_MinLen",
                Expression = $"LEN([{colName}]) >= {minLen.Length}",
                Description = $"Min length of {colName} is {minLen.Length} characters",
                ReferencedColumns = referenced
            });
        }

        // ✅ Range
        var range = prop.GetCustomAttribute<RangeAttribute>();
        if (range?.Minimum != null && range.Maximum != null)
        {
            string expr = range.Minimum is DateTime minDate && range.Maximum is DateTime maxDate
                ? $"[{colName}] BETWEEN '{minDate:yyyy-MM-dd}' AND '{maxDate:yyyy-MM-dd}'"
                : $"[{colName}] BETWEEN {range.Minimum} AND {range.Maximum}";

            result.Add(new CheckConstraintDefinition
            {
                Name = $"CK_{Normalize(entityName)}_{Normalize(colName)}_Range",
                Expression = expr,
                Description = $"{colName} must be between {range.Minimum} and {range.Maximum}",
                ReferencedColumns = referenced
            });
        }

        return result;
    }
    private static bool IsStringType(Type type) =>
        type == typeof(string) || type == typeof(char);

    private static List<IndexDefinition> GetIndexes(Type entityType)
    {
        var entityName = entityType.Name;
        var indexes = new List<IndexDefinition>();

        var allAttrs = entityType.GetCustomAttributes(inherit: true);

        foreach (var attr in allAttrs)
        {
            var typeName = attr.GetType().FullName;

            if (typeName == "System.ComponentModel.DataAnnotations.Schema.IndexAttribute" ||
                typeName == "Microsoft.EntityFrameworkCore.IndexAttribute")
            {
                var name = attr.GetPropertyValue<string>("Name") ?? $"IX_{entityName}";
                var isUnique = attr.GetPropertyValue<bool>("IsUnique");
                var columns = attr.GetPropertyValue<IEnumerable<string>>("PropertyNames")?.ToList()
                            ?? attr.GetPropertyValue<IEnumerable<string>>("Columns")?.ToList()
                            ?? new List<string>();

                var include = attr.GetPropertyValue<IEnumerable<string>>("IncludeProperties")?.ToList();
                var filter = attr.GetPropertyValue<string>("Filter");
                var description = attr.GetPropertyValue<string>("Description");
                var isFullText = attr.GetPropertyValue<bool>("IsFullText");

                if (columns.Count == 0) continue;

                indexes.Add(new IndexDefinition
                {
                    Name = name ?? $"IX_{entityName}_{string.Join("_", columns)}",
                    Columns = columns,
                    IsUnique = isUnique,
                    IncludeColumns = include,
                    FilterExpression = filter,
                    Description = description,
                    IsFullText = isFullText
                });
            }
        }

        return indexes;
    }

    /// <summary>
    /// Builds a column definition from a property, applying attributes, defaults, computed logic,
    /// and smart length adjustments for indexed text columns.
    /// </summary>
    public void BuildColumn(PropertyInfo prop, EntityDefinition entity)
    {
        // ⛔ تجاهل الخصائص التنقلية
        if (IsCollectionOfEntity(prop) || IsReferenceToEntity(prop))
            return;

        var columnName = GetColumnName(prop);

        // 📌 Nullable من النوع أو [Required]
        bool isNullable = IsNullable(prop);
        if (prop.GetCustomAttribute<RequiredAttribute>() != null)
            isNullable = false;

        // 📌 الطول من Attributes
        int? maxLength = null;
        var strLenAttr = prop.GetCustomAttribute<StringLengthAttribute>();
        var maxLenAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
        if (strLenAttr?.MaximumLength > 0)
            maxLength = strLenAttr.MaximumLength;
        else if (maxLenAttr?.Length > 0)
            maxLength = maxLenAttr.Length;
        else
            maxLength = GetMaxLength(prop);

        // 📌 Precision/Scale
        int? precision = null;
        int? scale = null;
        var precisionAttr = prop.GetCustomAttribute<PrecisionAttribute>();
        if (precisionAttr != null)
        {
            precision = precisionAttr.Precision;
            scale = precisionAttr.Scale;
        }

        // 📌 النوع SQL
        string typeName = null;
        var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
        if (colAttr != null && !string.IsNullOrWhiteSpace(colAttr.TypeName))
            typeName = colAttr.TypeName;
        else
            typeName = prop.PropertyType.MapClrTypeToSql(maxLength, precision, scale);

        // 📌 القيمة الافتراضية
        string defaultSql = null;
        var sqlDefAttr = prop.GetCustomAttribute<SqlDefaultValueAttribute>();
        if (sqlDefAttr != null && !string.IsNullOrWhiteSpace(sqlDefAttr.Expression))
            defaultSql = sqlDefAttr.Expression;
        else
        {
            var defValAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
            if (defValAttr != null)
                defaultSql = defValAttr.Value.ToSqlLiteral(typeName);
            else
            {
                var dv = GetDefaultValue(prop);
                if (dv != null)
                    defaultSql = dv.ToSqlLiteral(typeName);
            }
        }

        // 📌 ComputedAttribute
        var computedAttr = prop.GetCustomAttribute<ComputedAttribute>();
        string computedExpression = null;
        bool isPersisted = false;
        if (computedAttr != null)
        {
            computedExpression = computedAttr.SqlExpression;
            isPersisted = computedAttr.IsPersisted;
        }

        // 🛡️ تجاهل Default لو العمود Computed
        if (computedAttr != null && !string.IsNullOrWhiteSpace(defaultSql))
        {
            Console.WriteLine($"[WARN] {entity.Name}.{columnName} is computed, ignoring default value '{defaultSql}'.");
            defaultSql = null;
        }

        // 📌 إنشاء ColumnModel
        var columnModel = new ColumnModel
        {
            Name = columnName,
            PropertyType = prop.PropertyType,
            IsNullable = isNullable,
            MaxLength = maxLength,
            DefaultValue = defaultSql,
            TypeName = typeName,
            ComputedExpression = computedExpression,
            IsPersisted = isPersisted
        };

        // 📌 تطبيق الـ Handlers
        foreach (var handler in _handlers)
            handler.Apply(prop, columnModel);

        if (columnModel.IsIgnored) return;

        // 🆕 منطق التعديل الذكي للأعمدة النصية max
        if (IsTextType(columnModel.TypeName) && IsMaxLength(columnModel.TypeName))
        {
            bool hasLengthAttr =
                prop.GetCustomAttribute<StringLengthAttribute>() != null ||
                prop.GetCustomAttribute<MaxLengthAttribute>() != null;

            if (hasLengthAttr || columnModel.MaxLength != null)
            {
                int targetLength = columnModel.MaxLength ?? 450;
                columnModel.TypeName = $"nvarchar({targetLength})";

                // فحص حجم الفهرس
                bool indexTooLarge = columnModel.Indexes != null && columnModel.Indexes.Any(ix =>
                    (targetLength * 2) > 900 // nvarchar = 2 bytes per char
                );

                if (indexTooLarge)
                {
                    Console.WriteLine($"[WARN] {entity.Name}.{columnModel.Name} length {targetLength} may exceed index key size limit — index creation skipped, but column length updated in DB.");
                    columnModel.Indexes?.Clear();
                }
                else
                {
                    Console.WriteLine($"[AUTO-FIX] Changed {entity.Name}.{columnModel.Name} from nvarchar(max) to nvarchar({targetLength}) based on attribute.");
                }
            }
            else if (columnModel.Indexes != null && columnModel.Indexes.Count > 0)
            {
                // Auto-fix بدون Attribute
                int safeLength = 450;
                columnModel.TypeName = $"nvarchar({safeLength})";
                columnModel.MaxLength = safeLength;
                Console.WriteLine($"[AUTO-FIX] Changed {entity.Name}.{columnModel.Name} from nvarchar(max) to nvarchar({safeLength}) for indexing safety.");
            }
        }

        // 📌 تحويل إلى ColumnDefinition
        var columnDef = ToColumnDefinition(columnModel);
        columnDef.IsNavigationProperty = IsCollectionOfEntity(prop) || IsReferenceToEntity(prop);

        if (prop.HasIdentityAttribute() || prop.GetCustomAttribute<KeyAttribute>() != null)
            columnDef.IsIdentity = true;

        var colDescAttr = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        if (colDescAttr != null && !string.IsNullOrWhiteSpace(colDescAttr.Description))
            columnDef.Description = colDescAttr.Description;

        Console.WriteLine(
            $"[TRACE:ColumnInit] {entity.Name}.{columnDef.Name} → Identity={columnDef.IsIdentity}, Nullable={columnDef.IsNullable}, Type={columnDef.TypeName}, Default={(columnDef.DefaultValue ?? "NULL")}, Computed={(columnDef.ComputedExpression ?? "No")}, Persisted={columnDef.IsPersisted}"
        );

        entity.Columns.Add(columnDef);

        // 📌 Computed columns
        if (!string.IsNullOrWhiteSpace(columnModel.ComputedExpression))
        {
            var isIndexable = IsIndexableExpression(columnModel.ComputedExpression);
            entity.ComputedColumns.Add(new ComputedColumnDefinition
            {
                Name = columnName,
                Expression = columnModel.ComputedExpression,
                IsIndexable = isIndexable,
                IsPersisted = columnModel.IsPersisted
            });

            Console.WriteLine($"[TRACE:Computed] {columnName} → Expression={columnModel.ComputedExpression}, Persisted={columnModel.IsPersisted}, Indexable={isIndexable}");
        }

        // 📌 Check constraints
        var checks = ToCheckConstraints(prop, entity.Name);
        entity.CheckConstraints.AddRange(checks);
        foreach (var ck in checks)
            Console.WriteLine($"[TRACE:Check] {ck.Name} → {ck.Expression}");
    }

    /// <summary>
    /// Checks if the SQL type is a text-based type.
    /// </summary>
    private bool IsTextType(string typeName) =>
        typeName.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase) ||
        typeName.StartsWith("varchar", StringComparison.OrdinalIgnoreCase) ||
        typeName.StartsWith("varbinary", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the SQL type has a max length.
    /// </summary>
    private bool IsMaxLength(string typeName) =>
        typeName.Contains("(max)", StringComparison.OrdinalIgnoreCase);


    /// <summary>
    /// Converts index definitions from ColumnModel to IndexDefinition.
    /// </summary>
    private static IEnumerable<IndexDefinition> ToIndexes(ColumnModel model, string entityName)
    {
        var indexes = model.Indexes.Select(i => new IndexDefinition
        {
            Name = i.Name ?? $"IX_{entityName}_{model.Name}",
            Columns = new List<string> { model.Name },
            IsUnique = i.IsUnique,
            Description = i.Description,
            IncludeColumns = i.IncludeColumns?.ToList(),
            FilterExpression = i.FilterExpression,
            IsFullText = i.IsFullText
        }).ToList();

        if (model.IsUnique)
        {
            indexes.Add(new IndexDefinition
            {
                Name = model.UniqueConstraintName ?? $"UQ_{entityName}_{model.Name}",
                Columns = new List<string> { model.Name },
                IsUnique = true,
                Description = model.Description
            });
        }

        return indexes;
    }




    /// <summary>
    /// Extracts primary key columns from [Key] attributes.
    /// </summary>
    private static PrimaryKeyDefinition? GetPrimaryKey(Type type)
    {
        // 1️⃣ البحث عن [Key] Attributes
        var pkColumns = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<KeyAttribute>() != null)
            .Select(p => p.Name)
            .ToList();

        if (pkColumns.Count > 0)
        {
            Console.WriteLine($"[TRACE:PK] Final PK for {type.Name} (via [Key]): {string.Join(", ", pkColumns)}");

            return new PrimaryKeyDefinition
            {
                Columns = pkColumns,
                IsAutoGenerated = true,
                Name = $"PK_{type.Name}"
            };
        }

        // 2️⃣ fallback للأسماء الشائعة
        string typeNameId = type.Name + "Id";

        pkColumns = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p =>
                p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals(typeNameId, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        if (pkColumns.Count > 0)
        {
            Console.WriteLine($"[TRACE:PK] Final PK for {type.Name} (via naming): {string.Join(", ", pkColumns)}");

            return new PrimaryKeyDefinition
            {
                Columns = pkColumns,
                IsAutoGenerated = true,
                Name = $"PK_{type.Name}"
            };
        }

        // 3️⃣ استنتاج PK مركب للجداول الوسيطة
        var allProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                           .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                           .ToList();

        Console.WriteLine($"[TRACE:PK] Columns in {type.Name}: {string.Join(", ", allProps.Select(p => p.Name))}");

        var idCols = allProps
            .Where(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        Console.WriteLine($"[TRACE:PK] Id-like columns in {type.Name}: {string.Join(", ", idCols)}");

        if (idCols.Count >= 2)
        {
            Console.WriteLine($"[TRACE:PK] Composite PK inferred for {type.Name}: {string.Join(", ", idCols)}");

            return new PrimaryKeyDefinition
            {
                Columns = idCols,
                IsAutoGenerated = false,
                Name = $"PK_{type.Name}"
            };
        }

        // 4️⃣ لو مفيش حاجة
        Console.WriteLine($"[TRACE:PK] No PK found for {type.Name}");
        return null;
    }
    internal static void ApplyPrimaryKeyOverrides(EntityDefinition entity)
    {
        if (entity.PrimaryKey?.Columns != null && entity.PrimaryKey.Columns.Any())
        {
            bool isComposite = entity.PrimaryKey.Columns.Count > 1;

            foreach (var pkName in entity.PrimaryKey.Columns)
            {
                var pkCol = entity.Columns.FirstOrDefault(c =>
                    c.Name.Equals(pkName, StringComparison.OrdinalIgnoreCase));

                if (pkCol != null)
                {
                    pkCol.IsNullable = false;

                    pkCol.IsIdentity = !isComposite && entity.PrimaryKey.IsAutoGenerated;

                    Console.WriteLine($"[TRACE:PK] ApplyOverride → {entity.Name}.{pkCol.Name}: Identity={pkCol.IsIdentity}, Composite={isComposite}, Auto={entity.PrimaryKey.IsAutoGenerated}");
                }
            }
        }
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


    private ISchemaAttributeHandler[] _attributeseHandlers => new ISchemaAttributeHandler[]
    {
            new KeyAttributeHandler(),
            new SqlDefaultValueHandler(),
            new IndexAttributeHandler(),
            new DefaultValueAttributeHandler(),
            new DescriptionAttributeHandler(),
            new RequiredAttributeHandler(),
            new MaxLengthAttributeHandler(),
            new ComputedAttributeHandler(),
            new CollationAttributeHandler(),
            new CheckConstraintAttributeHandler(),
            new IgnoreColumnAttributeHandler(),
            new EfCompatibilityAttributeHandler(),
            new PrecisionAttributeHandler(),
            new CommentAttributeHandler(),
            new ForeignKeyAttributeHandler()
    };

    ///// <summary>
    ///// Infers SQL type name from CLR type if not explicitly provided.
    ///// </summary>
    //private static string InferSqlType(Type clrType)
    //{
    //    return clrType switch
    //    {
    //        Type t when t == typeof(string) => "nvarchar(max)",
    //        Type t when t == typeof(int) => "int",
    //        Type t when t == typeof(bool) => "bit",
    //        Type t when t == typeof(DateTime) => "datetime",
    //        Type t when t == typeof(decimal) => "decimal(18,2)",
    //        Type t when t == typeof(Guid) => "uniqueidentifier",
    //        _ => "nvarchar(max)" // fallback
    //    };
    //}


}
#endregion