using Syn.Core.SqlSchemaGenerator.Execution;
using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Builders;


/// <summary>
/// Coordinates schema comparison and migration execution between entity definitions or CLR types.
/// Uses SqlAlterTableBuilder to generate migration scripts and AutoMigrate to execute them.
/// Supports multiple execution modes including DryRun, Interactive, Preview, AutoMerge, PreMigrationReport, and ImpactAnalysis.
/// Also supports batch migration for multiple entities.
/// </summary>
public class MigrationService
{
    private readonly EntityDefinitionBuilder _entityDefinitionBuilder;
    private readonly SqlAlterTableBuilder _alterTableBuilder;
    private readonly AutoMigrate _autoMigrate;
    private readonly DatabaseSchemaReader _dbReader;
    /// <summary>
    /// Initializes the MigrationService with required builders and migration executor.
    /// </summary>
    public MigrationService(EntityDefinitionBuilder entityDefinitionBuilder, AutoMigrate autoMigrate, DatabaseSchemaReader dbReader)
    {
        _entityDefinitionBuilder = entityDefinitionBuilder ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        _alterTableBuilder = new SqlAlterTableBuilder(entityDefinitionBuilder, autoMigrate._connectionString);
        _autoMigrate = autoMigrate ?? throw new ArgumentNullException(nameof(autoMigrate));
        _dbReader = dbReader ?? throw new ArgumentNullException(nameof(dbReader));
    }


    /// <summary>
    /// Builds and optionally executes a migration script between two CLR types.
    /// Uses DB schema for the old entity and full relationship-aware build for the new entity.
    /// </summary>
    public string BuildMigrationScript(
        Type oldType,
        Type newType,
        bool execute = false,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false)
    {
        Console.WriteLine($"[MIGRATION] Building migration script from {oldType.Name} → {newType.Name}");

        var oldEntity = LoadEntityFromDatabase(oldType);
        var newEntity = _entityDefinitionBuilder
            .BuildAllWithRelationships(new[] { newType })
            .First();

        return BuildMigrationScript(
            oldEntity,
            newEntity,
            execute,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis
        );
    }

    /// <summary>
    /// Builds and optionally executes a migration script between two EntityDefinitions.
    /// </summary>
    public string BuildMigrationScript(
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        bool execute = false,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false)
    {
        Console.WriteLine($"[MIGRATION] Building migration script from {oldEntity.Name} → {newEntity.Name}");

        var script = _alterTableBuilder.Build(oldEntity, newEntity);

        if (string.IsNullOrWhiteSpace(script))
        {
            Console.WriteLine("[MIGRATION] No differences found. Nothing to migrate.");
            return "-- No changes detected.";
        }

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

        return script;
    }

    /// <summary>
    /// Executes migration for a batch of CLR types and prints a session summary.
    /// Uses DB schema for old entities and full relationship-aware build for new entities.
    /// </summary>
    public void RunBatchMigration(
        IEnumerable<Type> entityTypes,
        bool execute = false,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false)
    {
        var newEntities = _entityDefinitionBuilder.BuildAllWithRelationships(entityTypes).ToList();
        var entityPairs = newEntities.Select(ne => (LoadEntityFromDatabase(ne.ClrType ?? Type.GetType(ne.Name)), ne));

        RunBatchMigrationInternal(entityPairs, execute, dryRun, interactive, previewOnly, autoMerge, showReport, impactAnalysis);
    }
    /// <summary>
    /// Executes migration for a batch of EntityDefinition pairs and prints a session summary.
    /// 
    /// Each pair consists of:
    /// - oldEntity: The current schema definition loaded from the database.
    /// - newEntity: The target schema definition built from code (preferably via BuildAllWithRelationships).
    /// 
    /// For each entity pair:
    /// 1. Builds the migration script using <see cref="BuildMigrationScript(EntityDefinition, EntityDefinition, ...)"/>.
    /// 2. Classifies the change as:
    ///    - New table (no columns or constraints in oldEntity)
    ///    - Altered table (differences found)
    ///    - Unchanged table (no differences)
    /// 3. Optionally executes the migration script if <paramref name="execute"/> is true.
    /// 
    /// At the end, prints a summary of how many tables were new, altered, or unchanged.
    /// </summary>
    /// <param name="entityPairs">List of (oldEntity, newEntity) pairs to process.</param>
    /// <param name="execute">If true, executes the migration script after building it.</param>
    /// <param name="dryRun">If true, simulates execution without applying changes.</param>
    /// <param name="interactive">If true, runs in interactive mode with user prompts.</param>
    /// <param name="previewOnly">If true, shows the script without executing it.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge non-conflicting changes.</param>
    /// <param name="showReport">If true, prints a pre-migration report for each entity.</param>
    /// <param name="impactAnalysis">If true, performs impact analysis on the migration.</param>
    public void RunBatchMigration(
        IEnumerable<(EntityDefinition oldEntity, EntityDefinition newEntity)> entityPairs,
        bool execute = false,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false)
    {
        RunBatchMigrationInternal(entityPairs, execute, dryRun, interactive, previewOnly, autoMerge, showReport, impactAnalysis);
    }

    /// <summary>
    /// Compares two EntityDefinitions and returns a structured report of differences.
    /// No execution is performed. Useful for dry analysis or external reporting.
    /// 
    /// Steps:
    /// 1. Builds the ALTER script between the old and new entity definitions.
    /// 2. Splits the script into individual SQL commands.
    /// 3. Formats the output according to the specified format:
    ///    - "text": Plain text summary
    ///    - "markdown": Markdown-formatted summary
    ///    - "json": JSON object with detailed changes
    /// </summary>
    /// <param name="oldEntity">The current schema definition from the database.</param>
    /// <param name="newEntity">The target schema definition from code or external source.</param>
    /// <param name="format">Optional format: "text", "markdown", or "json". Default is "text".</param>
    /// <returns>Formatted report string describing schema differences.</returns>
    public string CompareOnly(EntityDefinition oldEntity, EntityDefinition newEntity, string format = "text")
    {
        // قبل أي Run جديد أو قبل توليد التقرير
        HelperMethod._suppressedWarnings.Clear();

        var script = _alterTableBuilder.Build(oldEntity, newEntity);
        var commands = SplitSqlCommands(script);

        if (string.IsNullOrWhiteSpace(script))
            return $"✅ No changes detected between [{oldEntity.Name}] and [{newEntity.Name}].";

        if (format == "markdown")
            return GenerateMarkdownReport(oldEntity, newEntity, commands);

        if (format == "json")
            return GenerateJsonReport(oldEntity, newEntity, commands);

        return GenerateTextReport(oldEntity, newEntity, commands);
        ;
    }

    /// <summary>
    /// Compares a batch of EntityDefinition pairs and returns a structured report of all differences.
    /// No execution is performed. Useful for dry analysis, CI pipelines, or external reporting.
    /// 
    /// Steps:
    /// 1. Iterates over each (oldEntity, newEntity) pair.
    /// 2. Builds the ALTER script and splits it into SQL commands.
    /// 3. Formats the output according to the specified format:
    ///    - "text": Plain text summary for each entity
    ///    - "markdown": Markdown-formatted summary for each entity
    ///    - "json": JSON array with detailed changes for all entities
    /// </summary>
    /// <param name="entityPairs">List of (oldEntity, newEntity) pairs to compare.</param>
    /// <param name="format">Optional format: "text", "markdown", or "json". Default is "text".</param>
    /// <returns>Formatted report string describing schema differences across all entities.</returns>
    public string CompareBatchOnly(
        IEnumerable<(EntityDefinition oldEntity, EntityDefinition newEntity)> entityPairs,
        string format = "text")
    {
        // قبل أي Run جديد أو قبل توليد التقرير
        HelperMethod._suppressedWarnings.Clear();

        var allReports = new List<string>();
        var allChanges = new List<object>();

        foreach (var (oldEntity, newEntity) in entityPairs)
        {
            var script = _alterTableBuilder.Build(oldEntity, newEntity);
            var commands = SplitSqlCommands(script);

            if (string.IsNullOrWhiteSpace(script))
            {
                allReports.Add($"✅ {newEntity.Name}: No changes detected.");
                allChanges.Add(new { Entity = newEntity.Name, Changes = new List<string>() });
                continue;
            }

            if (format == "markdown")
            {
                allReports.Add(GenerateMarkdownReport(oldEntity, newEntity, commands));
            }
            else if (format == "json")
            {
                allChanges.Add(new
                {
                    Entity = newEntity.Name,
                    Schema = newEntity.Schema,
                    Changes = commands.Select(c => new
                    {
                        Summary = c.Split('\n')[0].Trim(),
                        FullCommand = c
                    }).ToList()
                });
            }
            else
            {
                allReports.Add(GenerateTextReport(oldEntity, newEntity, commands));
            }
        }

        if (format == "json")
        {
            return System.Text.Json.JsonSerializer.Serialize(allChanges, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        return string.Join("\n\n", allReports);
    }

    /// <summary>
    /// Delegates safety analysis of a migration script to AutoMigrate.
    /// Splits the script into individual SQL commands and checks each one
    /// for potentially unsafe operations (e.g., data loss, destructive changes).
    /// </summary>
    /// <param name="migrationScript">The full SQL migration script to analyze.</param>
    /// <returns>
    /// A <see cref="MigrationSafetyResult"/> object containing:
    /// - IsSafe: Boolean indicating if all commands are safe.
    /// - Reasons: List of reasons for unsafe commands (if any).
    /// </returns>

    public MigrationSafetyResult AnalyzeMigrationSafety(string migrationScript)
    {
        var commands = migrationScript
            .Replace("\r", "")
            .Split(new[] { "\nGO\n", "\nGO ", "\nGO\r", "\nGO" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(cmd => cmd.Trim())
            .Where(cmd => !string.IsNullOrWhiteSpace(cmd))
            .ToList();

        return _autoMigrate.AnalyzeMigrationSafety(commands);
    }

    /// <summary>
    /// Internal unified implementation for running batch migrations.
    /// This method contains the core logic for iterating over entity pairs,
    /// building migration scripts, classifying changes, and printing a summary.
    /// </summary>
    /// <param name="entityPairs">A collection of (oldEntity, newEntity) pairs to process.</param>
    /// <param name="execute">If true, executes the migration script after building it.</param>
    /// <param name="dryRun">If true, simulates execution without applying changes.</param>
    /// <param name="interactive">If true, runs in interactive mode with user prompts.</param>
    /// <param name="previewOnly">If true, shows the script without executing it.</param>
    /// <param name="autoMerge">If true, attempts to auto-merge non-conflicting changes.</param>
    /// <param name="showReport">If true, prints a pre-migration report for each entity.</param>
    /// <param name="impactAnalysis">If true, performs impact analysis on the migration.</param>
    private void RunBatchMigrationInternal(
    IEnumerable<(EntityDefinition oldEntity, EntityDefinition newEntity)> entityPairs,
    bool execute,
    bool dryRun,
    bool interactive,
    bool previewOnly,
    bool autoMerge,
    bool showReport,
    bool impactAnalysis)
    {
        int newTables = 0;
        int alteredTables = 0;
        int unchangedTables = 0;

        // قبل أي Run جديد أو قبل توليد التقرير
        HelperMethod._suppressedWarnings.Clear();

        Console.WriteLine("=== Batch Migration Started ===");

        foreach (var (oldEntity, newEntity) in entityPairs)
        {
            Console.WriteLine($"\n[MIGRATION] Processing: {newEntity.Name}");

            var script = BuildMigrationScript(
                oldEntity,
                newEntity,
                execute,
                dryRun,
                interactive,
                previewOnly,
                autoMerge,
                showReport,
                impactAnalysis
            );

            if (string.IsNullOrWhiteSpace(script) || script.Contains("-- No changes detected."))
            {
                Console.WriteLine($"✅ {newEntity.Name}: No changes detected.");
                unchangedTables++;
            }
            else if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
            {
                Console.WriteLine($"🆕 {newEntity.Name}: New table will be created.");
                newTables++;
            }
            else
            {
                Console.WriteLine($"🔧 {newEntity.Name}: Table will be altered.");
                alteredTables++;
            }
        }

        Console.WriteLine("\n=== Batch Migration Completed ===");
        Console.WriteLine("📊 Session Summary:");
        Console.WriteLine($"🆕 New tables: {newTables}");
        Console.WriteLine($"🔧 Altered tables: {alteredTables}");
        Console.WriteLine($"✅ Unchanged tables: {unchangedTables}");
    }


    /// <summary>
    /// Generates a plain text comparison report between two entities.
    /// 
    /// The report includes:
    /// - A header with the entity names.
    /// - The total number of detected changes.
    /// - A bullet list of the first line of each SQL command.
    /// 
    /// This format is suitable for console output or simple logging.
    /// </summary>
    /// <param name="oldEntity">The current schema definition from the database.</param>
    /// <param name="newEntity">The target schema definition from code or external source.</param>
    /// <param name="commands">List of SQL commands representing the changes.</param>
    /// <returns>A formatted plain text report.</returns>
    private string GenerateTextReport(EntityDefinition oldEntity, EntityDefinition newEntity, List<string> commands)
    {
        var report = $"📋 Comparison Report: {oldEntity.Name} → {newEntity.Name}\n";
        report += $"Total changes: {commands.Count}\n";

        foreach (var cmd in commands)
        {
            var firstLine = cmd.Split('\n')[0].Trim();
            report += $" - {firstLine}\n";
        }

        if (HelperMethod._suppressedWarnings.Count > 0)
        {
            report += "\n⚠️ Suppressed Warnings (already reported in previous run):\n";
            foreach (var warn in HelperMethod._suppressedWarnings.Distinct().OrderBy(w => w))
                report += $"   {warn}\n";
        }

        return report;
    }


    /// <summary>
    /// Generates a Markdown-formatted comparison report between two entities.
    /// 
    /// The report includes:
    /// - A Markdown header with the entity names.
    /// - The total number of detected changes.
    /// - A bullet list of the first line of each SQL command, formatted as inline code.
    /// 
    /// This format is suitable for GitHub issues, pull requests, or Markdown-based documentation.
    /// </summary>
    /// <param name="oldEntity">The current schema definition from the database.</param>
    /// <param name="newEntity">The target schema definition from code or external source.</param>
    /// <param name="commands">List of SQL commands representing the changes.</param>
    /// <returns>A formatted Markdown report.</returns>
    private string GenerateMarkdownReport(EntityDefinition oldEntity, EntityDefinition newEntity, List<string> commands)
    {
        var md = $"## 📋 Comparison Report: `{oldEntity.Name}` → `{newEntity.Name}`\n";
        md += $"**Total changes:** {commands.Count}\n\n";

        foreach (var cmd in commands)
        {
            var firstLine = cmd.Split('\n')[0].Trim();
            md += $"- `{firstLine}`\n";
        }

        if (HelperMethod._suppressedWarnings.Count > 0)
        {
            md += "\n### ⚠️ Suppressed Warnings (already reported in previous run)\n";
            foreach (var warn in HelperMethod._suppressedWarnings.Distinct().OrderBy(w => w))
                md += $"- `{warn}`\n";
        }

        return md;
    }



    /// <summary>
    /// Generates a JSON-formatted comparison report between two entities.
    /// 
    /// The JSON object includes:
    /// - Entity: The name of the new entity.
    /// - Schema: The schema name of the new entity.
    /// - Changes: A list of objects, each containing:
    ///   - Summary: The first line of the SQL command.
    ///   - FullCommand: The complete SQL command.
    /// 
    /// This format is suitable for programmatic consumption, APIs, or CI/CD pipelines.
    /// </summary>
    /// <param name="oldEntity">The current schema definition from the database.</param>
    /// <param name="newEntity">The target schema definition from code or external source.</param>
    /// <param name="commands">List of SQL commands representing the changes.</param>
    /// <returns>A JSON string containing the structured report.</returns>
    private string GenerateJsonReport(EntityDefinition oldEntity, EntityDefinition newEntity, List<string> commands)
    {
        var json = new
        {
            Entity = newEntity.Name,
            Schema = newEntity.Schema,
            Changes = commands.Select(c => new
            {
                Summary = c.Split('\n')[0].Trim(),
                FullCommand = c
            }).ToList(),
            SuppressedWarnings = HelperMethod._suppressedWarnings.Distinct().OrderBy(w => w).ToList()
        };

        return System.Text.Json.JsonSerializer.Serialize(json, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }


    /// <summary>
    /// Splits a SQL migration script into individual commands.
    /// 
    /// The script is split using common "GO" batch separators, and each command is trimmed.
    /// Empty or whitespace-only commands are removed.
    /// 
    /// This method is used by comparison and safety analysis methods to process scripts command-by-command.
    /// </summary>
    /// <param name="script">The full SQL migration script.</param>
    /// <returns>A list of individual SQL commands.</returns>
    private List<string> SplitSqlCommands(string script)
    {
        return script
            .Replace("\r", "")
            .Split(new[] { "\nGO\n", "\nGO ", "\nGO\r", "\nGO" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(cmd => cmd.Trim())
            .Where(cmd => !string.IsNullOrWhiteSpace(cmd))
            .ToList();
    }

    /// <summary>
    /// Loads the current EntityDefinition from the database for a given EntityDefinition (new model).
    /// Uses Schema and Table name from the entity itself.
    /// If the table is missing, returns an empty placeholder to treat it as new.
    /// </summary>
    internal EntityDefinition LoadEntityFromDatabase(Type type)
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
    /// Loads the current EntityDefinition from the database for a given EntityDefinition (new model).
    /// Uses Schema and Table name from the entity itself.
    /// If the table is missing, returns an empty placeholder to treat it as new.
    /// </summary>
    internal EntityDefinition LoadEntityFromDatabase(EntityDefinition newEntity)
    {
        var schema = newEntity.Schema;
        var table = newEntity.Name;

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
}