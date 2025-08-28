using Microsoft.Data.SqlClient;

using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;

using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator;

using Syn.Core.SqlSchemaGenerator.Helper;

using System;
using System.Collections.Generic;

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
        _migrationService = new MigrationService(_entityDefinitionBuilder, _autoMigrate);
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
        _migrationService = new MigrationService(builder, autoMigrate);
    }

    /// <summary>
    /// Loads the current EntityDefinition from the database for a given CLR type.
    /// Extracts schema and table name from attributes if available.
    /// If the table is missing, returns an empty placeholder to treat it as new.
    /// </summary>
    private EntityDefinition LoadEntityFromDatabase(Type type)
    {
        var (schema, table) = type.GetTableInfo();

        Console.WriteLine($"[DB Loader] Loading schema for table [{schema}].[{table}]");

        var entity = _dbReader.GetEntityDefinition(schema, table);

        if (entity == null)
        {
            Console.WriteLine($"?? [DB Loader] Table [{schema}].[{table}] not found in DB. Marked as NEW.");
            return new EntityDefinition
            {
                Schema = schema,
                Name = table,
                Columns = new List<ColumnDefinition>(),
                Constraints = new List<ConstraintDefinition>(),
                CheckConstraints = new List<CheckConstraintDefinition>(),
                Indexes = new List<IndexDefinition>()
            };
        }

        return entity;
    }

    /// <summary>
    /// Runs a migration session for a list of CLR entity types.
    /// Compares each entity with its database version, generates migration script,
    /// shows detailed reports, analyzes safety, and optionally executes.
    /// </summary>
    public void RunMigrationSession(
        IEnumerable<Type> entityTypes,
        bool execute = true,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false)
    {
        Console.WriteLine("=== Migration Runner Started ===");

        int newTables = 0;
        int alteredTables = 0;
        int unchangedTables = 0;

        foreach (var entityType in entityTypes)
        {
            Console.WriteLine($"\n[RUNNER] Processing entity: {entityType.Name}");

            try
            {
                var oldEntity = LoadEntityFromDatabase(entityType);
                var newEntity = _entityDefinitionBuilder.Build(entityType);

                var script = _migrationService.BuildMigrationScript(
                    oldEntity,
                    newEntity,
                    execute: false, // defer execution until after safety check
                    dryRun,
                    interactive,
                    previewOnly,
                    autoMerge,
                    showReport,
                    impactAnalysis
                );

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
                    _autoMigrate.Execute(
                        script,
                        oldEntity,
                        newEntity,
                        dryRun,
                        interactive,
                        previewOnly,
                        autoMerge,
                        showReport,
                        impactAnalysis
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [RUNNER] Migration failed for {entityType.Name}: {ex.Message}");
            }
        }

        Console.WriteLine("\n=== Migration Runner Completed ===");
        Console.WriteLine("📊 Summary:");
        Console.WriteLine($"🆕 New tables created: {newTables}");
        Console.WriteLine($"🔧 Tables altered: {alteredTables}");
        Console.WriteLine($"✅ Unchanged tables: {unchangedTables}");
    }
}


