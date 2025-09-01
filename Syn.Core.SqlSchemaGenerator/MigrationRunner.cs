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
    /// Runs a migration session for a list of CLR entity types.
    /// Compares each entity with its database version, generates migration script,
    /// analyzes impact and safety, shows detailed reports, and optionally executes interactively.
    /// </summary>
    public void RunMigrationSession(
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


