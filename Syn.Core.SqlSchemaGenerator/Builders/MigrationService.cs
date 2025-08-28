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

    /// <summary>
    /// Initializes the MigrationService with required builders and migration executor.
    /// </summary>
    public MigrationService(EntityDefinitionBuilder entityDefinitionBuilder, AutoMigrate autoMigrate)
    {
        _entityDefinitionBuilder = entityDefinitionBuilder ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        _alterTableBuilder = new SqlAlterTableBuilder(entityDefinitionBuilder);
        _autoMigrate = autoMigrate ?? throw new ArgumentNullException(nameof(autoMigrate));
    }

    /// <summary>
    /// Builds and optionally executes a migration script between two CLR types.
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

        var oldEntity = _entityDefinitionBuilder.Build(oldType);
        var newEntity = _entityDefinitionBuilder.Build(newType);

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
        int newTables = 0;
        int alteredTables = 0;
        int unchangedTables = 0;

        Console.WriteLine("=== Batch Migration Started ===");

        foreach (var type in entityTypes)
        {
            Console.WriteLine($"\n[MIGRATION] Processing: {type.Name}");

            var oldEntity = _entityDefinitionBuilder.Build(type); // Replace with DB reader if needed
            var newEntity = _entityDefinitionBuilder.Build(type);

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
                Console.WriteLine($"✅ {type.Name}: No changes detected.");
                unchangedTables++;
            }
            else if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
            {
                Console.WriteLine($"🆕 {type.Name}: New table will be created.");
                newTables++;
            }
            else
            {
                Console.WriteLine($"🔧 {type.Name}: Table will be altered.");
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
    /// Executes migration for a batch of EntityDefinition pairs and prints a session summary.
    /// </summary>
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
        int newTables = 0;
        int alteredTables = 0;
        int unchangedTables = 0;

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
    /// Compares two EntityDefinitions and returns a structured report of differences.
    /// No execution is performed. Useful for dry analysis or external reporting.
    /// </summary>
    /// <param name="oldEntity">The current schema definition from the database.</param>
    /// <param name="newEntity">The target schema definition from code or external source.</param>
    /// <param name="format">Optional format: "text", "markdown", or "json".</param>
    /// <returns>Formatted report string describing schema differences.</returns>
    public string CompareOnly(EntityDefinition oldEntity, EntityDefinition newEntity, string format = "text")
    {
        var script = _alterTableBuilder.Build(oldEntity, newEntity);
        var commands = SplitSqlCommands(script);

        if (string.IsNullOrWhiteSpace(script))
            return $"✅ No changes detected between [{oldEntity.Name}] and [{newEntity.Name}].";

        if (format == "markdown")
            return GenerateMarkdownReport(oldEntity, newEntity, commands);

        if (format == "json")
            return GenerateJsonReport(oldEntity, newEntity, commands);

        return GenerateTextReport(oldEntity, newEntity, commands);
    }

    /// <summary>
    /// Compares a batch of EntityDefinition pairs and returns a structured report of all differences.
    /// No execution is performed. Useful for dry analysis, CI pipelines, or external reporting.
    /// </summary>
    /// <param name="entityPairs">List of (oldEntity, newEntity) pairs to compare.</param>
    /// <param name="format">Optional format: "text", "markdown", or "json".</param>
    /// <returns>Formatted report string describing schema differences across all entities.</returns>
    public string CompareBatchOnly(
        IEnumerable<(EntityDefinition oldEntity, EntityDefinition newEntity)> entityPairs,
        string format = "text")
    {
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
    /// Returns a structured result with safe/unsafe commands and reasons.
    /// </summary>
    /// <param name="commands">List of SQL commands to analyze.</param>
    /// <returns>Structured result indicating safety and reasons.</returns>
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

    private string GenerateTextReport(EntityDefinition oldEntity, EntityDefinition newEntity, List<string> commands)
    {
        var report = $"📋 Comparison Report: {oldEntity.Name} → {newEntity.Name}\n";
        report += $"Total changes: {commands.Count}\n";

        foreach (var cmd in commands)
        {
            var firstLine = cmd.Split('\n')[0].Trim();
            report += $" - {firstLine}\n";
        }

        return report;
    }

    private string GenerateMarkdownReport(EntityDefinition oldEntity, EntityDefinition newEntity, List<string> commands)
    {
        var md = $"## 📋 Comparison Report: `{oldEntity.Name}` → `{newEntity.Name}`\n";
        md += $"**Total changes:** {commands.Count}\n\n";

        foreach (var cmd in commands)
        {
            var firstLine = cmd.Split('\n')[0].Trim();
            md += $"- `{firstLine}`\n";
        }

        return md;
    }

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
            }).ToList()
        };

        return System.Text.Json.JsonSerializer.Serialize(json, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private List<string> SplitSqlCommands(string script)
    {
        return script
            .Replace("\r", "")
            .Split(new[] { "\nGO\n", "\nGO ", "\nGO\r", "\nGO" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(cmd => cmd.Trim())
            .Where(cmd => !string.IsNullOrWhiteSpace(cmd))
            .ToList();
    }
}