
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
    /// <summary>
    /// Builds an <see cref="EntityDefinition"/> model from the specified entity <see cref="Type"/>.
    /// This method scans the entity type's public instance properties, applies column handlers,
    /// and populates all relevant metadata, including columns, computed columns, constraints,
    /// indexes, and optional descriptions. The resulting definition is ready for SQL generation
    /// using the unified <c>Build(EntityDefinition)</c> method.
    /// </summary>
    /// <param name="entityType">The CLR type representing the entity to analyze.</param>
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

        // 📋 Table-level description
        var tableDescAttr = entityType.GetCustomAttribute<DescriptionAttribute>();
        if (tableDescAttr != null && !string.IsNullOrWhiteSpace(tableDescAttr.Text))
            entity.Description = tableDescAttr.Text;

        // 🧩 Columns & property analysis
        foreach (var prop in entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null))
        {
            // 🛡 فلترة الملاحة
            if (IsCollectionOfEntity(prop) || IsReferenceToEntity(prop))
                continue;

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
                handler.Apply(prop, columnModel);

            if (columnModel.IsIgnored) continue;

            var columnDef = ToColumnDefinition(columnModel);

            // الأولوية هنا لـ Attribute كإضافة فقط
            if (prop.HasIdentityAttribute())
                columnDef.IsIdentity = true;

            var colDescAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            if (colDescAttr != null && !string.IsNullOrWhiteSpace(colDescAttr.Text))
                columnDef.Description = colDescAttr.Text;

            // 🔍 تتبع قبل إضافة العمود
            Console.WriteLine($"[TRACE:ColumnInit] {entity.Name}.{columnDef.Name} → Identity={columnDef.IsIdentity}, Nullable={columnDef.IsNullable}, Type={columnDef.TypeName}");

            entity.Columns.Add(columnDef);

            // Computed columns
            if (!string.IsNullOrWhiteSpace(columnModel.ComputedExpression))
            {
                entity.ComputedColumns.Add(new ComputedColumnDefinition
                {
                    Name = columnName,
                    Expression = columnModel.ComputedExpression
                });
            }

            // CHECK constraints from column model
            var checks = ToCheckConstraints(columnModel, entity.Name);
            entity.CheckConstraints.AddRange(checks);

            // Indexes from column model
            var indexes = ToIndexes(columnModel, entity.Name);
            entity.Indexes.AddRange(indexes);
        }

        // 🎯 Primary key أولاً
        entity.PrimaryKey = GetPrimaryKey(entityType);

        // 🆕 لو فيه PK من غير اسم → توليد اسم افتراضي
        if (entity.PrimaryKey != null && string.IsNullOrWhiteSpace(entity.PrimaryKey.Name))
        {
            entity.PrimaryKey.Name = $"PK_{entity.Name}";
        }

        // 🆕 أولوية الـ PK override
        ApplyPrimaryKeyOverrides(entity);

        // 🔍 تتبع بعد تطبيق الـ override
        foreach (var col in entity.Columns)
        {
            Console.WriteLine($"[TRACE:ColumnPostOverride] {entity.Name}.{col.Name} → Identity={col.IsIdentity}, Nullable={col.IsNullable}");
        }

        // Unique constraints
        entity.UniqueConstraints = GetUniqueConstraints(entityType);

        // Explicit foreign keys
        entity.ForeignKeys = entityType.GetForeignKeys();
        foreach (var fk in entity.ForeignKeys)
        {
            if (string.IsNullOrWhiteSpace(fk.ConstraintName))
                fk.ConstraintName = $"FK_{entity.Name}_{fk.Column}";
        }

        // علاقات من الـ Navigation Properties
        InferForeignKeysFromNavigation(entityType, entity);

        // One-to-One relationships
        InferOneToOneRelationships(entityType, entity, new List<EntityDefinition> { entity });

        // CHECK constraints from Attributes
        InferCheckConstraints(entityType, entity);

        // Validate FKs
        ValidateForeignKeys(entity);

        // Class-level indexes
        var classLevelIndexes = GetIndexes(entityType);
        entity.Indexes.AddRange(classLevelIndexes);

        // إزالة الفهارس المكررة
        entity.Indexes = entity.Indexes
            .GroupBy(ix => ix.Name)
            .Select(g => g.First())
            .ToList();

        // Trace
        Console.WriteLine($"[TRACE] Built entity: {entity.Name}");
        Console.WriteLine("  Columns:");
        foreach (var col in entity.Columns)
            Console.WriteLine($"    🧩 {col.Name} ({col.TypeName}) Nullable={col.IsNullable} Identity={col.IsIdentity}");

        Console.WriteLine("  Relationships:");
        foreach (var rel in entity.Relationships)
            Console.WriteLine($"    🔗 {rel.SourceEntity} {rel.Type} -> {rel.TargetEntity} (Cascade={rel.OnDelete})");

        Console.WriteLine("  CheckConstraints:");
        foreach (var ck in entity.CheckConstraints)
            Console.WriteLine($"    ✅ {ck.Name}: {ck.Expression}");

        return entity;
    }    // Helper methods
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

        var result = new List<EntityDefinition>();
        foreach (var type in entityTypes)
        {
            result.Add(Build(type));
        }
        return result;
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
    public List<EntityDefinition> BuildAllWithRelationships(IEnumerable<Type> entityTypes)
    {
        if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));

        Console.WriteLine("===== [TRACE] Pass 1: Building basic entities =====");

        // 🧠 تتبع الكيانات اللي داخلة فعليًا
        foreach (var type in entityTypes)
        {
            Console.WriteLine($"[TRACE:Build] Including type: {type.Name}");
        }

        // 🥇 Pass 1: بناء الكيانات وتجميع البيانات الأساسية فقط
        var allEntities = entityTypes
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
            .Select(t =>
            {
                var entity = Build(t); // بناء أولي
                entity.ClrType = t;

                Console.WriteLine($"[TRACE] Built entity: {entity.Name}");
                Console.WriteLine("  Columns:");
                foreach (var col in entity.Columns)
                    Console.WriteLine($"    🧩 {col.Name} ({col.TypeName}) Nullable={col.IsNullable} Identity={col.IsIdentity}");
                Console.WriteLine($"  Relationships: {entity.Relationships.Count}");
                Console.WriteLine($"  CheckConstraints: {entity.CheckConstraints.Count}");

                return entity;
            })
            .ToList();

        // 🥈 Pass 2: نسخة Snapshot آمنة
        var entityListSnapshot = allEntities.ToList();

        Console.WriteLine();
        Console.WriteLine("===== [TRACE] Pass 2: Inferring relationships and constraints =====");

        foreach (var entity in entityListSnapshot)
        {
            Console.WriteLine($"[TRACE] Analyzing {entity.Name}...");

            // 🔹 Foreign Keys من الـ Navigation
            InferForeignKeysFromNavigation(entity.ClrType, entity);

            // 🔹 علاقات One-to-Many و Many-to-Many
            InferCollectionRelationships(entity.ClrType, entity, allEntities);

            // 🔹 علاقات One-to-One مع تتبّع
            Console.WriteLine($"  -> Before OneToOne: {entity.Relationships.Count} relationships");
            InferOneToOneRelationships(entity.ClrType, entity, allEntities);
            Console.WriteLine($"  -> After OneToOne: {entity.Relationships.Count} relationships");

            // 🔹 قيود CHECK مع تتبّع
            Console.WriteLine($"  -> Before CHECK: {entity.CheckConstraints.Count} constraints");
            InferCheckConstraints(entity.ClrType, entity);
            Console.WriteLine($"  -> After CHECK: {entity.CheckConstraints.Count} constraints");
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
            Description = model.Description,
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


    private ISchemaAttributeHandler[] _attributeseHandlers => new ISchemaAttributeHandler[]
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
            new PrecisionAttributeHandler(),
            new CommentAttributeHandler(),
            new ForeignKeyAttributeHandler()
    };

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