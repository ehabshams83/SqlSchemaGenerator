using Microsoft.Data.SqlClient;

using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;
using System.Reflection;

using Microsoft.EntityFrameworkCore;

using Syn.Core.SqlSchemaGenerator.Execution;
using Syn.Core.SqlSchemaGenerator.Extensions;

namespace Syn.Core.SqlSchemaGenerator;
/// <summary>
/// Orchestrates schema comparison and migration execution for a batch of CLR entities.
/// Supports full reporting, safety analysis, and execution modes.
/// </summary>
public class MigrationRunner
{
    private readonly EntityDefinitionBuilder _entityDefinitionBuilder;
    private readonly AutoMigrate _autoMigrate;
    private readonly MigrationService _migrationService;
    private readonly DatabaseSchemaReader _dbReader;

    /// <summary>
    /// Default constructor: builds all components from connection string.
    /// </summary>
    public MigrationRunner(string connectionString)
    {
        _entityDefinitionBuilder = new EntityDefinitionBuilder();
        _autoMigrate = new AutoMigrate(connectionString);
        var connection = new SqlConnection(connectionString);
        _dbReader = new DatabaseSchemaReader(connection);
        _migrationService = new MigrationService(_entityDefinitionBuilder, _autoMigrate, _dbReader);
    }

    /// <summary>
    /// Custom constructor: allows injecting components manually.
    /// Useful for testing or advanced configuration.
    /// </summary>
    public MigrationRunner(EntityDefinitionBuilder builder, AutoMigrate autoMigrate, DatabaseSchemaReader dbReader)
    {
        _entityDefinitionBuilder = builder ?? throw new ArgumentNullException(nameof(builder));
        _autoMigrate = autoMigrate ?? throw new ArgumentNullException(nameof(autoMigrate));
        _dbReader = dbReader ?? throw new ArgumentNullException(nameof(dbReader));
        _migrationService = new MigrationService(builder, autoMigrate, dbReader);
    }

    /// <summary>
    /// Scans a single assembly for entity types, optionally filtered by one or more base classes or interfaces,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <param name="filterTypes">Optional filter types (interfaces or base classes).</param>
    public void ApplyEntityDefinitionsToModel(ModelBuilder builder, Assembly assembly, params Type[] filterTypes)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly.FilterTypesFromAssembly(filterTypes);
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }

    /// <summary>
    /// Scans a single assembly for entity types assignable to the specified generic type,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <typeparam name="T">Base class or interface to filter entity types.</typeparam>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    public void ApplyEntityDefinitionsToModel<T>(ModelBuilder builder, Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly.FilterTypesFromAssembly(typeof(T));
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }

    /// <summary>
    /// Scans a single assembly for entity types assignable to both specified generic types,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <typeparam name="T1">First base class or interface to filter entity types.</typeparam>
    /// <typeparam name="T2">Second base class or interface to filter entity types.</typeparam>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    public void ApplyEntityDefinitionsToModel<T1, T2>(ModelBuilder builder, Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        var entityTypes = assembly.FilterTypesFromAssembly(typeof(T1), typeof(T2));
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }


    /// <summary>
    /// Scans multiple assemblies for entity types, optionally filtered by one or more base classes or interfaces,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assemblies">The assemblies to scan for entity types.</param>
    /// <param name="filterTypes">Optional filter types (interfaces or base classes).</param>
    public void ApplyEntityDefinitionsToModel(ModelBuilder builder, IEnumerable<Assembly> assemblies, params Type[] filterTypes)
    {
        if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

        var entityTypes = assemblies.FilterTypesFromAssemblies(filterTypes);
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }

    /// <summary>
    /// Scans multiple assemblies for entity types assignable to the specified generic type,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <typeparam name="T">Base class or interface to filter entity types.</typeparam>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assemblies">The assemblies to scan for entity types.</param>
    public void ApplyEntityDefinitionsToModel<T>(ModelBuilder builder, IEnumerable<Assembly> assemblies)
    {
        if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

        var entityTypes = assemblies.FilterTypesFromAssemblies(typeof(T));
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }

    /// <summary>
    /// Scans multiple assemblies for entity types assignable to both specified generic types,
    /// then applies their definitions to the EF Core ModelBuilder.
    /// </summary>
    /// <typeparam name="T1">First base class or interface to filter entity types.</typeparam>
    /// <typeparam name="T2">Second base class or interface to filter entity types.</typeparam>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="assemblies">The assemblies to scan for entity types.</param>
    public void ApplyEntityDefinitionsToModel<T1, T2>(ModelBuilder builder, IEnumerable<Assembly> assemblies)
    {
        if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

        var entityTypes = assemblies.FilterTypesFromAssemblies(typeof(T1), typeof(T2));
        ApplyEntityDefinitionsToModel(builder, entityTypes);
    }


    /// <summary>
    /// Builds entity definitions from the provided CLR types (including relationships)
    /// and applies them to the EF Core ModelBuilder.
    /// Configures:
    /// - Columns (type, nullability, precision, defaults, computed columns, collation, comments)
    /// - Primary keys, unique constraints, indexes, check constraints
    /// - Relationships (One-to-One, One-to-Many, Many-to-One, Many-to-Many)
    /// - Foreign keys automatically from column metadata
    /// </summary>
    /// <param name="builder">The EF Core ModelBuilder instance.</param>
    /// <param name="entityTypes">The CLR types representing entities to configure.</param>
    public void ApplyEntityDefinitionsToModel(ModelBuilder builder, IEnumerable<Type> entityTypes)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));

        // Build entity definitions with relationships
        var entities = _entityDefinitionBuilder
            .BuildAllWithRelationships(entityTypes)
            .ToList();

        foreach (var entity in entities)
        {
            var entityBuilder = builder.Entity(entity.ClrType);

            // =========================
            // Configure Columns
            // =========================
            foreach (var column in entity.Columns)
            {
                var propertyBuilder = entityBuilder.Property(column.PropertyType, column.Name);

                // Nullability
                if (!column.IsNullable)
                    propertyBuilder.IsRequired();

                // Precision / Scale
                if (column.Precision.HasValue && column.Scale.HasValue)
                    propertyBuilder.HasPrecision(column.Precision.Value, column.Scale.Value);
                else if (column.Precision.HasValue)
                    propertyBuilder.HasPrecision(column.Precision.Value);

                // Default value
                if (column.DefaultValue != null)
                    propertyBuilder.HasDefaultValue(column.DefaultValue);

                // Computed column
                if (!string.IsNullOrWhiteSpace(column.ComputedExpression))
                    propertyBuilder.HasComputedColumnSql(column.ComputedExpression, column.IsPersisted);

                // Collation
                if (!string.IsNullOrWhiteSpace(column.Collation))
                    propertyBuilder.UseCollation(column.Collation);

                // Comment / Description
                if (!string.IsNullOrWhiteSpace(column.Comment))
                    propertyBuilder.HasComment(column.Comment);
                else if (!string.IsNullOrWhiteSpace(column.Description))
                    propertyBuilder.HasComment(column.Description);

                // Primary key
                if (column.IsPrimaryKey)
                    entityBuilder.HasKey(column.Name);

                // Unique constraint
                if (column.IsUnique)
                {
                    var idx = entityBuilder.HasIndex(column.Name).IsUnique();
                    if (!string.IsNullOrWhiteSpace(column.UniqueConstraintName))
                        idx.HasDatabaseName(column.UniqueConstraintName);
                }

                // ✅ Updated Check constraints (new EF Core API)
                foreach (var check in column.CheckConstraints)
                {
                    entityBuilder.ToTable(tb =>
                    {
                        tb.HasCheckConstraint(check.Name, check.Expression);
                    });
                }

                // Indexes
                foreach (var index in column.Indexes)
                {
                    var idxBuilder = entityBuilder.HasIndex(index.Columns.ToArray());
                    if (index.IsUnique)
                        idxBuilder.IsUnique();
                    if (!string.IsNullOrWhiteSpace(index.Name))
                        idxBuilder.HasDatabaseName(index.Name);
                }

                // Auto-configure Foreign Keys from column metadata
                if (column.IsForeignKey && !string.IsNullOrWhiteSpace(column.ForeignKeyTarget))
                {
                    var targetEntity = entities.FirstOrDefault(e =>
                        string.Equals(e.Name, column.ForeignKeyTarget, StringComparison.OrdinalIgnoreCase));

                    if (targetEntity != null)
                    {
                        entityBuilder
                            .HasOne(targetEntity.ClrType)
                            .WithMany()
                            .HasForeignKey(column.Name);
                    }
                }
            }

            // =========================
            // Configure Relationships
            // =========================
            foreach (var rel in entity.Relationships)
            {
                var sourceEntityDef = entities.FirstOrDefault(e =>
                    string.Equals(e.Name, rel.SourceEntity, StringComparison.OrdinalIgnoreCase));
                var targetEntityDef = entities.FirstOrDefault(e =>
                    string.Equals(e.Name, rel.TargetEntity, StringComparison.OrdinalIgnoreCase));

                if (sourceEntityDef == null || targetEntityDef == null)
                    continue;

                var sourceBuilder = builder.Entity(sourceEntityDef.ClrType);

                var deleteBehavior = MapDeleteBehavior(rel.OnDelete);

                switch (rel.Type)
                {
                    case RelationshipType.OneToMany:
                        sourceBuilder
                            .HasMany(targetEntityDef.ClrType, rel.SourceProperty)
                            .WithOne(rel.TargetProperty)
                            .HasForeignKey(rel.SourceToTargetColumn)
                            .IsRequired(rel.IsRequired)
                            .OnDelete(deleteBehavior);
                        break;

                    case RelationshipType.ManyToOne:
                        sourceBuilder
                            .HasOne(targetEntityDef.ClrType, rel.SourceProperty)
                            .WithMany(rel.TargetProperty)
                            .HasForeignKey(rel.SourceToTargetColumn)
                            .IsRequired(rel.IsRequired)
                            .OnDelete(deleteBehavior);
                        break;

                    case RelationshipType.OneToOne:
                        sourceBuilder
                            .HasOne(targetEntityDef.ClrType, rel.SourceProperty)
                            .WithOne(rel.TargetProperty)
                            .HasForeignKey(sourceEntityDef.ClrType, rel.SourceToTargetColumn)
                            .IsRequired(rel.IsRequired)
                            .OnDelete(deleteBehavior);
                        break;

                    case RelationshipType.ManyToMany:
                        var manyToMany = sourceBuilder
                            .HasMany(targetEntityDef.ClrType, rel.SourceProperty)
                            .WithMany(rel.TargetProperty);

                        if (!string.IsNullOrWhiteSpace(rel.JoinEntityName))
                        {
                            manyToMany.UsingEntity(rel.JoinEntityName);
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Maps a System.Data ReferentialAction to EF Core DeleteBehavior.
    /// </summary>
    private DeleteBehavior MapDeleteBehavior(ReferentialAction action)
    {
        return action switch
        {
            ReferentialAction.Cascade => DeleteBehavior.Cascade,
            ReferentialAction.SetNull => DeleteBehavior.SetNull,
            ReferentialAction.Restrict => DeleteBehavior.Restrict,
            _ => DeleteBehavior.NoAction
        };
    }

    /// <summary>
    /// Runs a migration session for all entity types found in the provided assemblies,
    /// filtered by one or more generic type parameters (interfaces or base classes).
    /// </summary>
    /// <typeparam name="T">First filter type (interface or base class).</typeparam>
    /// <param name="assemblies">Assemblies to scan for entity types.</param>
    /// <param name="execute">Whether to execute the migration scripts after generation.</param>
    /// <param name="dryRun">If true, scripts are generated but not executed.</param>
    /// <param name="interactive">If true, runs in interactive mode (step-by-step execution).</param>
    /// <param name="previewOnly">If true, shows the generated scripts without executing.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge changes.</param>
    /// <param name="showReport">If true, displays a pre-migration report.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, attempts rollback on failure.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback scripts.</param>
    /// <param name="interactiveMode">Interactive mode type ("step" or "batch").</param>
    /// <param name="rollbackPreviewOnly">If true, shows rollback scripts without executing.</param>
    /// <param name="logToFile">If true, logs migration details to a file.</param>
    public void Initiate<T>(
        IEnumerable<Assembly> assemblies,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false)
    {
        var entityTypes = assemblies.FilterTypesFromAssemblies(typeof(T));

        Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile
        );
    }

    /// <summary>
    /// Runs a migration session for all entity types found in the provided assemblies,
    /// filtered by one or more generic type parameters (interfaces or base classes).
    /// </summary>
    /// <typeparam name="T1">First filter type (interface or base class).</typeparam>
    /// <typeparam name="T2">Second filter type (interface or base class).</typeparam>
    /// <param name="assemblies">Assemblies to scan for entity types.</param>
    /// <param name="execute">Whether to execute the migration scripts after generation.</param>
    /// <param name="dryRun">If true, scripts are generated but not executed.</param>
    /// <param name="interactive">If true, runs in interactive mode (step-by-step execution).</param>
    /// <param name="previewOnly">If true, shows the generated scripts without executing.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge changes.</param>
    /// <param name="showReport">If true, displays a pre-migration report.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, attempts rollback on failure.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback scripts.</param>
    /// <param name="interactiveMode">Interactive mode type ("step" or "batch").</param>
    /// <param name="rollbackPreviewOnly">If true, shows rollback scripts without executing.</param>
    /// <param name="logToFile">If true, logs migration details to a file.</param>
    public void Initiate<T1, T2>(
        IEnumerable<Assembly> assemblies,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false)
    {
        var entityTypes = assemblies.FilterTypesFromAssemblies(
            typeof(T1),
            typeof(T2)
        );

        Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile
        );
    }


    /// <summary>
    /// Runs a migration session for all entity types found in the provided assemblies,
    /// optionally filtered by one or more interfaces or base classes.
    /// </summary>
    /// <param name="assembly">Assembly to scan for entity types.</param>
    /// <param name="execute">Whether to execute the migration scripts after generation.</param>
    /// <param name="dryRun">If true, scripts are generated but not executed.</param>
    /// <param name="interactive">If true, runs in interactive mode (step-by-step execution).</param>
    /// <param name="previewOnly">If true, shows the generated scripts without executing.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge changes.</param>
    /// <param name="showReport">If true, displays a pre-migration report.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, attempts rollback on failure.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback scripts.</param>
    /// <param name="interactiveMode">Interactive mode type ("step" or "batch").</param>
    /// <param name="rollbackPreviewOnly">If true, shows rollback scripts without executing.</param>
    /// <param name="logToFile">If true, logs migration details to a file.</param>
    /// <param name="filterTypes">
    /// Optional filter types (interfaces or base classes). Only types assignable to at least one of these will be included.
    /// </param>
    public void Initiate(
    Assembly assembly,
    bool execute = true,
    bool dryRun = false,
    bool interactive = false,
    bool previewOnly = false,
    bool autoMerge = false,
    bool showReport = false,
    bool impactAnalysis = false,
    bool rollbackOnFailure = true,
    bool autoExecuteRollback = false,
    string interactiveMode = "step",
    bool rollbackPreviewOnly = false,
    bool logToFile = false,
    params Type[] filterTypes)
    {
        var entityTypes = assembly.FilterTypesFromAssembly(filterTypes);

        Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile
        );
    }

    /// <summary>
    /// Runs a migration session for all entity types found in the provided assemblies,
    /// optionally filtered by one or more interfaces or base classes.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for entity types.</param>
    /// <param name="execute">Whether to execute the migration scripts after generation.</param>
    /// <param name="dryRun">If true, scripts are generated but not executed.</param>
    /// <param name="interactive">If true, runs in interactive mode (step-by-step execution).</param>
    /// <param name="previewOnly">If true, shows the generated scripts without executing.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge changes.</param>
    /// <param name="showReport">If true, displays a pre-migration report.</param>
    /// <param name="impactAnalysis">If true, performs an impact analysis before migration.</param>
    /// <param name="rollbackOnFailure">If true, attempts rollback on failure.</param>
    /// <param name="autoExecuteRollback">If true, automatically executes rollback scripts.</param>
    /// <param name="interactiveMode">Interactive mode type ("step" or "batch").</param>
    /// <param name="rollbackPreviewOnly">If true, shows rollback scripts without executing.</param>
    /// <param name="logToFile">If true, logs migration details to a file.</param>
    /// <param name="filterTypes">
    /// Optional filter types (interfaces or base classes). Only types assignable to at least one of these will be included.
    /// </param>
    public void Initiate(
        IEnumerable<Assembly> assemblies,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false,
        params Type[] filterTypes)
    {
        var entityTypes = assemblies.FilterTypesFromAssemblies(filterTypes);

        Initiate(
            entityTypes,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile
        );
    }


    /// <summary>
    /// Runs a migration session for a list of CLR entity types.
    /// Compares each entity with its database version, generates migration script,
    /// analyzes impact and safety, shows detailed reports, and optionally executes interactively.
    /// </summary>
    public void Initiate(
    IEnumerable<Type> entityTypes,
    bool execute = true,
    bool dryRun = false,
    bool interactive = false,
    bool previewOnly = false,
    bool autoMerge = false,
    bool showReport = false,
    bool impactAnalysis = false,
    bool rollbackOnFailure = true,
    bool autoExecuteRollback = false,
    string interactiveMode = "step",
    bool rollbackPreviewOnly = false,
    bool logToFile = false)
    {
        Console.WriteLine("=== Migration Runner Started ===");

        int newTables = 0;
        int alteredTables = 0;
        int unchangedTables = 0;

        // ✅ Pass 1+2+3: بناء كل الكيانات مرة واحدة
        var newEntities = _entityDefinitionBuilder.BuildAllWithRelationships(entityTypes).ToList();

        foreach (var newEntity in newEntities)
        {
            Console.WriteLine($"\n[RUNNER] Processing entity: {newEntity.ClrType?.Name ?? newEntity.Name}");

            try
            {
                var oldEntity = _migrationService.LoadEntityFromDatabase(newEntity);

                var script = _migrationService.BuildMigrationScript(
                    oldEntity,
                    newEntity,
                    execute: false,
                    dryRun,
                    interactive,
                    previewOnly,
                    autoMerge,
                    showReport,
                    impactAnalysis);

                var commands = _autoMigrate.SplitSqlCommands(script);
                var impact = impactAnalysis ? _autoMigrate.AnalyzeImpact(oldEntity, newEntity) : new();
                if (impactAnalysis) _autoMigrate.AssignSeverityAndReason(impact);

                // 🧠 Safety Analysis
                var safety = _migrationService.AnalyzeMigrationSafety(script);

                Console.WriteLine("\n🔍 Migration Safety Analysis:");
                if (safety.IsSafe)
                {
                    Console.WriteLine("✅ All commands are safe.");
                }
                else
                {
                    Console.WriteLine("⚠️ Unsafe commands detected:");
                    foreach (var reason in safety.Reasons)
                        Console.WriteLine($"   - {reason}");
                }

                // 📋 Show Report
                if (showReport)
                {
                    _autoMigrate.ShowPreMigrationReport(oldEntity, newEntity, commands, impact, impactAnalysis);
                    Console.WriteLine();
                }

                // 🧮 Classification
                if (string.IsNullOrWhiteSpace(script) || script.Contains("-- No changes detected."))
                {
                    unchangedTables++;
                }
                else if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
                {
                    newTables++;
                }
                else
                {
                    alteredTables++;
                }

                // 🚀 Execute if approved
                if (execute)
                {
                    if (interactive)
                    {
                        _autoMigrate.ExecuteInteractiveAdvanced(
                            script,
                            oldEntity,
                            newEntity,
                            rollbackOnFailure,
                            autoExecuteRollback,
                            interactiveMode,
                            rollbackPreviewOnly,
                            logToFile);
                    }
                    else
                    {
                        _autoMigrate.Execute(
                            script,
                            oldEntity,
                            newEntity,
                            dryRun,
                            interactive,
                            previewOnly,
                            autoMerge,
                            showReport,
                            impactAnalysis);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [RUNNER] Migration failed for {newEntity.Name}: {ex.Message}");
            }
        }

        Console.WriteLine("\n=== Migration Runner Completed ===");
        Console.WriteLine("📊 Summary:");
        Console.WriteLine($"🆕 New tables created: {newTables}");
        Console.WriteLine($"🔧 Tables altered: {alteredTables}");
        Console.WriteLine($"✅ Unchanged tables: {unchangedTables}");
    }

}


