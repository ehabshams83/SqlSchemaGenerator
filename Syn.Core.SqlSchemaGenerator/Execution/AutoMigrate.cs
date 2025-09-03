using Microsoft.Data.SqlClient;

using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Storage;

using System.Text;
using System.Text.RegularExpressions;

namespace Syn.Core.SqlSchemaGenerator.Execution;

/// <summary>
/// Executes SQL migration scripts with multiple safety and review modes:
/// - Normal: Direct execution in a transaction
/// - DryRun: Show commands only, no execution
/// - Interactive: Approve each command individually
/// - Preview: Show summarized changes before execution
/// - AutoMerge: Auto-execute if only safe additive changes
/// - PreMigrationReport: Detailed list of all changes before execution
/// - ImpactAnalysis: Warns about potential side effects of changes
/// </summary>
public class AutoMigrate
{
    internal readonly string _connectionString;

    public AutoMigrate(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Executes a migration script with full support for dry run, preview, interactive, auto merge, and reporting modes.
    /// Ensures the target schema exists before executing.
    /// </summary>
    public void Execute(
        string migrationScript,
        EntityDefinition oldEntity = null,
        EntityDefinition newEntity = null,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string logicalGroup = null,
        MigrationSettings settings = null)
    {
        ExecuteAsync(
            migrationScript,
            oldEntity,
            newEntity,
            dryRun,
            interactive,
            previewOnly,
            autoMerge,
            showReport,
            impactAnalysis,
            rollbackOnFailure,
            autoExecuteRollback,
            logicalGroup,
            settings
        ).GetAwaiter().GetResult();
    }



    /// <summary>
    /// Executes a migration script with full support for dry run, preview, interactive, auto merge, and reporting modes.
    /// Ensures the target schema exists before executing.
    /// </summary>
    public async Task ExecuteAsync(
        string migrationScript,
        EntityDefinition oldEntity = null,
        EntityDefinition newEntity = null,
        bool dryRun = false,
        bool interactive = false,
        bool previewOnly = false,
        bool autoMerge = false,
        bool showReport = false,
        bool impactAnalysis = false,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string logicalGroup = null,
        MigrationSettings settings = null)
    {
        if (string.IsNullOrWhiteSpace(migrationScript))
        {
            Console.WriteLine("[AutoMigrate] No SQL commands to process.");
            return;
        }

        var schema = newEntity?.Schema ?? "dbo";
        var impact = impactAnalysis ? AnalyzeImpact(oldEntity, newEntity) : new();

        AssignSeverityAndReason(impact);
        RenderImpactMarkdown(impact);
        RenderImpactHtml(impact);

        if (showReport)
        {
            ShowPreMigrationReport(oldEntity, newEntity, SplitSqlCommands(migrationScript), impact, impactAnalysis);
            Console.WriteLine();
        }

        if (dryRun)
        {
            Console.WriteLine("🔍 [AutoMigrate] Dry Run mode: No changes will be applied.");
            Console.WriteLine(migrationScript);
            return;
        }

        EnsureSchemaExists(schema);

        var snapshotStore = new JsonSchemaSnapshotStore(@"C:\Snapshots");
        var history = new MigrationHistoryStore(_connectionString, snapshotStore, settings ?? new MigrationSettings());

        var (isNewVersion, version) = history.EnsureTableAndInsertPending(migrationScript, newEntity, logicalGroup);
        if (!isNewVersion) return;

        var runner = new SqlScriptRunner();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (interactive)
            {
                await ExecuteInteractiveAdvancedAsync(migrationScript, oldEntity, newEntity, rollbackOnFailure, autoExecuteRollback, "step", false, false, logicalGroup, settings);
            }
            else
            {
                var result = await runner.ExecuteScriptAsync(_connectionString, migrationScript);
                Console.WriteLine($"✅ Executed {result.ExecutedBatches}/{result.TotalBatches} batches in {result.DurationMs} ms");
            }

            stopwatch.Stop();
            var allEntitiesAfterMigration = newEntity != null
                ? new List<EntityDefinition> { newEntity }
                : new List<EntityDefinition>();

            history.MarkApplied(version, (int)stopwatch.ElapsedMilliseconds, allEntitiesAfterMigration);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            history.MarkFailed(version, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes a migration script interactively, allowing step-by-step or batch execution,
    /// with optional rollback, logging, and impact analysis.
    /// </summary>
    /// <param name="migrationScript">The full SQL migration script to execute.</param>
    /// <param name="oldEntity">The previous version of the entity (used for rollback and impact analysis).</param>
    /// <param name="newEntity">The updated version of the entity (used for rollback and impact analysis).</param>
    /// <param name="rollbackOnFailure">If true, rolls back the transaction automatically on failure.</param>
    /// <param name="autoExecuteRollback">If true, prompts the user to execute rollback script after failure.</param>
    /// <param name="interactiveMode">Execution mode: "step" for command-by-command approval, "batch" for full execution.</param>
    /// <param name="rollbackPreviewOnly">If true, displays the rollback script without executing it.</param>
    /// <param name="logToFile">If true, saves execution log to "migration.log" in the current directory.</param>
    public void ExecuteInteractiveAdvanced(
        string migrationScript,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false,
        string logicalGroup = null,
        MigrationSettings settings = null)
    {
        ExecuteInteractiveAdvancedAsync(
            migrationScript,
            oldEntity,
            newEntity,
            rollbackOnFailure,
            autoExecuteRollback,
            interactiveMode,
            rollbackPreviewOnly,
            logToFile,
            logicalGroup,
            settings
        ).GetAwaiter().GetResult();
    }



    /// <summary>
    /// Executes a migration script interactively, allowing step-by-step or batch execution,
    /// with optional rollback, logging, and impact analysis.
    /// </summary>
    /// <param name="migrationScript">The full SQL migration script to execute.</param>
    /// <param name="oldEntity">The previous version of the entity (used for rollback and impact analysis).</param>
    /// <param name="newEntity">The updated version of the entity (used for rollback and impact analysis).</param>
    /// <param name="rollbackOnFailure">If true, rolls back the transaction automatically on failure.</param>
    /// <param name="autoExecuteRollback">If true, prompts the user to execute rollback script after failure.</param>
    /// <param name="interactiveMode">Execution mode: "step" for command-by-command approval, "batch" for full execution.</param>
    /// <param name="rollbackPreviewOnly">If true, displays the rollback script without executing it.</param>
    /// <param name="logToFile">If true, saves execution log to "migration.log" in the current directory.</param>
    public async Task ExecuteInteractiveAdvancedAsync(
        string migrationScript,
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        bool rollbackOnFailure = true,
        bool autoExecuteRollback = false,
        string interactiveMode = "step",
        bool rollbackPreviewOnly = false,
        bool logToFile = false,
        string logicalGroup = null,
        MigrationSettings settings = null)
    {
        if (string.IsNullOrWhiteSpace(migrationScript))
        {
            Console.WriteLine("[Interactive] No SQL commands to process.");
            return;
        }

        var snapshotStore = new JsonSchemaSnapshotStore(@"C:\Snapshots");
        var history = new MigrationHistoryStore(_connectionString, snapshotStore, settings ?? new MigrationSettings());
        var (isNewVersion, version) = history.EnsureTableAndInsertPending(migrationScript, newEntity, logicalGroup);
        if (!isNewVersion) return;

        var runner = new SqlScriptRunner();
        var log = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (interactiveMode == "batch")
            {
                var result = await runner.ExecuteScriptAsync(_connectionString, migrationScript);
                log.Add($"✅ Executed {result.ExecutedBatches}/{result.TotalBatches} batches in {result.DurationMs} ms");
            }
            else
            {
                var commands = SplitSqlCommands(migrationScript);
                foreach (var cmd in commands)
                {
                    Console.WriteLine($"\n🔍 Next Command:\n{cmd}\n");
                    Console.Write("Execute this command? (Y/N): ");
                    var input = Console.ReadLine()?.Trim().ToUpperInvariant();
                    if (input == "Y")
                    {
                        await runner.ExecuteScriptAsync(_connectionString, cmd);
                        log.Add($"✅ Executed: {cmd}");
                    }
                    else
                    {
                        log.Add($"⏭️ Skipped: {cmd}");
                    }
                }
            }

            stopwatch.Stop();
            var allEntitiesAfterMigration = newEntity != null
                ? new List<EntityDefinition> { newEntity }
                : new List<EntityDefinition>();

            history.MarkApplied(version, (int)stopwatch.ElapsedMilliseconds, allEntitiesAfterMigration);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            history.MarkFailed(version, ex.Message);

            if (rollbackOnFailure)
            {
                var rollbackScript = GenerateRollbackScript(AnalyzeImpact(oldEntity, newEntity));
                if (rollbackPreviewOnly)
                {
                    Console.WriteLine("📜 Rollback Preview:");
                    foreach (var r in rollbackScript)
                        Console.WriteLine(r);
                }
                else if (autoExecuteRollback)
                {
                    await runner.ExecuteScriptAsync(_connectionString, string.Join("\nGO\n", rollbackScript));
                }
            }
            throw;
        }
        finally
        {
            if (logToFile)
                File.WriteAllLines("migration.log", log);
        }
    }



    /// <summary>
    /// Assigns severity level and explanatory reason to each impact item
    /// based on its type and action.
    /// </summary>
    /// <param name="impact">The list of impact items to enrich.</param>
    public void AssignSeverityAndReason(List<ImpactItem> impact)
    {
        foreach (var item in impact)
        {
            switch (item.Type)
            {
                case "Column":
                    if (item.Action == "Dropped")
                    {
                        item.Severity = "High";
                        item.Reason = "Dropping this column may lead to permanent data loss.";
                    }
                    else if (item.Action == "Modified" && item.NewType?.Contains("NOT NULL") == true)
                    {
                        item.Severity = "High";
                        item.Reason = "Changing to NOT NULL may fail if existing rows contain NULL values.";
                    }
                    else if (item.Action == "Modified")
                    {
                        item.Severity = "Medium";
                        item.Reason = "Altering column type may affect data compatibility or indexing.";
                    }
                    else if (item.Action == "Added")
                    {
                        item.Severity = "Low";
                        item.Reason = "Adding a column is safe unless constraints are applied.";
                    }
                    break;

                case "Constraint":
                    if (item.Action == "Dropped" && item.Name.StartsWith("FK_", StringComparison.OrdinalIgnoreCase))
                    {
                        item.Severity = "High";
                        item.Reason = "Dropping a foreign key may break relational integrity.";
                    }
                    else if (item.Action == "Added" && item.Name.StartsWith("FK_", StringComparison.OrdinalIgnoreCase))
                    {
                        item.Severity = "Medium";
                        item.Reason = "Adding a foreign key may fail if existing data violates the relationship.";
                    }
                    else if (item.Action == "Modified")
                    {
                        item.Severity = "Medium";
                        item.Reason = "Changing constraint logic may affect validation or inserts.";
                    }
                    else
                    {
                        item.Severity = "Low";
                        item.Reason = "Adding or dropping a check constraint is usually safe.";
                    }
                    break;

                case "Index":
                    if (item.Action == "Dropped")
                    {
                        item.Severity = "Medium";
                        item.Reason = "Dropping index may degrade query performance.";
                    }
                    else if (item.Action == "Modified")
                    {
                        item.Severity = "Medium";
                        item.Reason = "Changing index structure may affect execution plans.";
                    }
                    else if (item.Action == "Added")
                    {
                        item.Severity = "Low";
                        item.Reason = "Adding index improves performance but may increase write cost.";
                    }
                    break;
            }
        }
    }


    /// Analyzes a list of SQL commands and returns a detailed safety report.
    /// </summary>
    /// <param name="commands">List of SQL commands to analyze.</param>
    /// <returns>Structured result indicating safety and reasons.</returns>
    public MigrationSafetyResult AnalyzeMigrationSafety(
        List<string> commands,
        EntityDefinition oldEntity = null,
        EntityDefinition newEntity = null)
    {
        var result = new MigrationSafetyResult { IsSafe = true };

        // التصنيف الأولي حسب النص (كما هو)
        foreach (var cmd in commands)
        {
            var upper = cmd.ToUpperInvariant();
            var summary = cmd.Split('\n')[0].Trim();

            if (upper.Contains("DROP COLUMN"))
            {
                result.IsSafe = false;
                result.UnsafeCommands.Add(cmd);
                result.Reasons.Add($"Dropping column → {summary}");
            }
            else if (upper.Contains("DROP CONSTRAINT"))
            {
                result.IsSafe = false;
                result.UnsafeCommands.Add(cmd);
                result.Reasons.Add($"Dropping constraint → {summary}");
            }
            else if (upper.Contains("ALTER COLUMN"))
            {
                result.IsSafe = false;
                result.UnsafeCommands.Add(cmd);
                result.Reasons.Add($"Altering column → {summary}");
            }
            else if (upper.Contains("DROP INDEX"))
            {
                result.IsSafe = false;
                result.UnsafeCommands.Add(cmd);
                result.Reasons.Add($"Dropping index → {summary}");
            }
            else if (upper.Contains("ALTER TABLE") && upper.Contains("DROP"))
            {
                result.IsSafe = false;
                result.UnsafeCommands.Add(cmd);
                result.Reasons.Add($"ALTER TABLE with DROP → {summary}");
            }
            else
            {
                result.SafeCommands.Add(cmd);
            }
        }

        // فلترة التحذيرات الكاذبة لو الـ entities متاحة
        if (oldEntity != null && newEntity != null)
        {
            var (droppedConstraints, addedConstraints) = DiffCheckConstraints(oldEntity, newEntity);
            var (droppedIndexes, addedIndexes) = DiffIndexes(oldEntity, newEntity);

            FilterSafeChanges(result, droppedConstraints, addedConstraints, droppedIndexes, addedIndexes);
        }

        return result;
    }

    private void ExecuteCommand(string sql)
    {
        using var connection = new SqlConnection(_connectionString);
        if (connection.State == System.Data.ConnectionState.Closed)
        {
            connection.Open();
        }

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }


    private static string Norm(string s) => (s ?? "").Trim().ToLowerInvariant();

    private (List<CheckConstraintDefinition> dropped, List<CheckConstraintDefinition> added)
        DiffCheckConstraints(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        var dropped = new List<CheckConstraintDefinition>();
        var added = new List<CheckConstraintDefinition>();

        var oldCks = oldEntity.CheckConstraints ?? new List<CheckConstraintDefinition>();
        var newCks = newEntity.CheckConstraints ?? new List<CheckConstraintDefinition>();

        // نبني مفاتيح مقارنة: الأعمدة المستهدفة + التعبير بعد Normalize
        string CkKey(CheckConstraintDefinition ck) =>
            string.Join(",", (ck.ReferencedColumns ?? new List<string>()).Select(Norm))
            + "||" + Norm(ck.Expression);

        var oldMap = oldCks.GroupBy(CkKey).ToDictionary(g => g.Key, g => g.ToList());
        var newMap = newCks.GroupBy(CkKey).ToDictionary(g => g.Key, g => g.ToList());

        // ما ليس له نظير في الجديد → Dropped
        foreach (var ck in oldCks)
            if (!newMap.ContainsKey(CkKey(ck))) dropped.Add(ck);

        // ما ليس له نظير في القديم → Added
        foreach (var ck in newCks)
            if (!oldMap.ContainsKey(CkKey(ck))) added.Add(ck);

        return (dropped, added);
    }

    private (List<IndexDefinition> dropped, List<IndexDefinition> added)
        DiffIndexes(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        var dropped = new List<IndexDefinition>();
        var added = new List<IndexDefinition>();

        var oldIdx = oldEntity.Indexes ?? new List<IndexDefinition>();
        var newIdx = newEntity.Indexes ?? new List<IndexDefinition>();

        // مفتاح المقارنة: الأعمدة (مرتبة) + IsUnique
        string IxKey(IndexDefinition ix) =>
            string.Join(",", (ix.Columns ?? new List<string>()).Select(Norm)) + "||" + ix.IsUnique;

        var oldMap = oldIdx.GroupBy(IxKey).ToDictionary(g => g.Key, g => g.ToList());
        var newMap = newIdx.GroupBy(IxKey).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var ix in oldIdx)
            if (!newMap.ContainsKey(IxKey(ix))) dropped.Add(ix);

        foreach (var ix in newIdx)
            if (!oldMap.ContainsKey(IxKey(ix))) added.Add(ix);

        return (dropped, added);
    }

    private bool IsSafeConstraintChange(CheckConstraintDefinition oldCk, CheckConstraintDefinition newCk)
    {
        // آمن لو نفس الأعمدة ونفس التعبير (بعد Normalize)
        bool colsEqual = (oldCk.ReferencedColumns ?? new List<string>())
            .SequenceEqual(newCk.ReferencedColumns ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        return colsEqual && Norm(oldCk.Expression) == Norm(newCk.Expression);
    }

    private bool IsSafeIndexChange(IndexDefinition oldIx, IndexDefinition newIx)
    {
        bool colsEqual = (oldIx.Columns ?? new List<string>())
            .SequenceEqual(newIx.Columns ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        return colsEqual && oldIx.IsUnique == newIx.IsUnique;
    }

    private void FilterSafeChanges(
        MigrationSafetyResult result,
        List<CheckConstraintDefinition> droppedConstraints,
        List<CheckConstraintDefinition> addedConstraints,
        List<IndexDefinition> droppedIndexes,
        List<IndexDefinition> addedIndexes)
    {
        // قيود CHECK الآمنة
        foreach (var drop in droppedConstraints)
        {
            var match = addedConstraints.FirstOrDefault(add => IsSafeConstraintChange(drop, add));
            if (match != null)
            {
                // نحاول نحذف الأوامر/الأسباب المرتبطة بالاسم أولاً، ثم بالعمود لو الاسم مش ظاهر في النص
                var keyName = !string.IsNullOrWhiteSpace(drop.Name) ? drop.Name : drop.ReferencedColumns?.FirstOrDefault() ?? "";
                if (!string.IsNullOrWhiteSpace(keyName))
                {
                    result.UnsafeCommands.RemoveAll(c =>
                        c.IndexOf(keyName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        c.IndexOf("CHECK", StringComparison.OrdinalIgnoreCase) >= 0);
                    result.Reasons.RemoveAll(r => r.IndexOf("Dropping constraint", StringComparison.OrdinalIgnoreCase) >= 0);
                }

                Console.WriteLine($"[TRACE:Safety] Ignored safe constraint change on {string.Join(",", drop.ReferencedColumns ?? new List<string>())}");
            }
        }

        // الفهارس الآمنة
        foreach (var dropIx in droppedIndexes)
        {
            var match = addedIndexes.FirstOrDefault(addIx => IsSafeIndexChange(dropIx, addIx));
            if (match != null)
            {
                var keyCol = dropIx.Columns?.FirstOrDefault() ?? "";
                if (!string.IsNullOrWhiteSpace(keyCol))
                {
                    result.UnsafeCommands.RemoveAll(c =>
                        c.IndexOf(keyCol, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        c.IndexOf("INDEX", StringComparison.OrdinalIgnoreCase) >= 0);
                    result.Reasons.RemoveAll(r => r.IndexOf("Dropping index", StringComparison.OrdinalIgnoreCase) >= 0);
                }

                Console.WriteLine($"[TRACE:Safety] Ignored safe index change on {string.Join(",", dropIx.Columns ?? new List<string>())}");
            }
        }

        // حدّث العلم IsSafe بعد التنقية
        if (result.UnsafeCommands.Count == 0 && result.Reasons.Count == 0)
            result.IsSafe = true;
    }

    /// <summary>
    /// Displays a detailed pre-migration report including new table structure,
    /// grouped SQL commands, and impact analysis warnings.
    /// </summary>
    /// <param name="oldEntity">The original entity definition (null if new table).</param>
    /// <param name="newEntity">The updated entity definition.</param>
    /// <param name="commands">The list of SQL commands to be executed.</param>
    /// <param name="impact">The list of impact items describing structural changes.</param>
    /// <param name="impactAnalysis">Whether to include impact warnings in the report.</param>
    public void ShowPreMigrationReport(
        EntityDefinition oldEntity,
        EntityDefinition newEntity,
        List<string> commands,
        List<ImpactItem> impact,
        bool impactAnalysis)
    {
        Console.WriteLine("📋 Pre‑Migration Report");
        Console.WriteLine("===================================");

        if (oldEntity != null && IsNewTable(oldEntity))
        {
            Console.WriteLine($"🆕 New Table: [{newEntity.Schema}].[{newEntity.Name}]");

            Console.WriteLine("   Columns:");
            foreach (var col in newEntity.Columns)
                Console.WriteLine($"     - {col.Name} ({col.TypeName}) {(col.IsNullable ? "NULL" : "NOT NULL")}{(col.IsIdentity ? " IDENTITY" : "")}");

            if (newEntity.Constraints.Any())
            {
                Console.WriteLine("   Constraints:");
                foreach (var c in newEntity.Constraints)
                    Console.WriteLine($"     - {c.Type} {c.Name} ({string.Join(", ", c.Columns)})");
            }

            if (newEntity.CheckConstraints.Any())
            {
                Console.WriteLine("   Check Constraints:");
                foreach (var chk in newEntity.CheckConstraints)
                    Console.WriteLine($"     - {chk.Name}: {chk.Expression}");
            }

            if (newEntity.Indexes.Any())
            {
                Console.WriteLine("   Indexes:");
                foreach (var idx in newEntity.Indexes)
                    Console.WriteLine($"     - {idx.Name} ({string.Join(", ", idx.Columns)}){(idx.IsUnique ? " UNIQUE" : "")}");
            }
        }
        else
        {
            GroupCommands("🆕 Added Columns/Constraints", commands, "ADD");
            GroupCommands("❌ Dropped Columns/Constraints", commands, "DROP");
            GroupCommands("🔧 Altered Columns", commands, "ALTER COLUMN");
            GroupCommands("📌 Index Changes", commands, "INDEX");
            GroupCommands("🔗 Foreign Keys", commands, "FOREIGN KEY");
            GroupCommands("✅ CHECK Constraints", commands, "CHECK");
        }

        if (impactAnalysis && impact?.Any() == true)
        {
            Console.WriteLine("\n⚠️ Impact Analysis Warnings:");
            Console.WriteLine("===================================");

            foreach (var item in impact)
            {
                switch (item.Type)
                {
                    case "Column":
                        if (item.Action == "Dropped")
                            Console.WriteLine($"   - {item.Name}: Dropping this column may lead to data loss.");
                        else if (item.Action == "Modified" && item.NewType?.Contains("NOT NULL") == true)
                            Console.WriteLine($"   - {item.Name}: Changing to NOT NULL may fail if NULL values exist.");
                        break;

                    case "Constraint":
                        if (item.Action == "Dropped" && item.Name.StartsWith("FK_", StringComparison.OrdinalIgnoreCase))
                            Console.WriteLine($"   - {item.Name}: Dropping this FK may break relational integrity.");
                        else if (item.Action == "Added" && item.Name.StartsWith("FK_", StringComparison.OrdinalIgnoreCase))
                            Console.WriteLine($"   - {item.Name}: Adding a FK may fail if existing data violates the relationship.");
                        break;

                    case "Index":
                        if (item.Action == "Dropped")
                            Console.WriteLine($"   - {item.Name}: Dropping index may affect query performance.");
                        else if (item.Action == "Modified")
                            Console.WriteLine($"   - {item.Name}: Index structure changed — may affect execution plans.");
                        break;
                }
            }
        }

        Console.WriteLine("===================================");
        Console.WriteLine($"Total commands: {commands.Count}");
    }

    public void RenderImpactMarkdown(List<ImpactItem> impact, string filePath = "impact.md")
    {
        var lines = new List<string>
    {
        "# 📋 Migration Impact Report",
        "",
        "| Type | Action | Table | Name | Severity | Reason |",
        "|------|--------|-------|------|----------|--------|"
    };

        foreach (var item in impact)
        {
            lines.Add($"| {item.Type} | {item.Action} | {item.Table} | {item.Name} | {item.Severity ?? "-"} | {item.Reason ?? "-"} |");
        }

        lines.Add("");
        lines.Add($"_Generated on {DateTime.Now:yyyy-MM-dd HH:mm}_");

        File.WriteAllLines(filePath, lines);
        Console.WriteLine($"📁 Markdown report saved to {filePath}");
    }

    public void RenderImpactHtml(List<ImpactItem> impact, string filePath = "impact.html")
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><meta charset='UTF-8'><title>Migration Impact Report</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial; margin: 20px; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; }");
        html.AppendLine("th, td { border: 1px solid #ccc; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #f2f2f2; }");
        html.AppendLine(".high { background-color: #ffe5e5; }");
        html.AppendLine(".medium { background-color: #fffbe5; }");
        html.AppendLine(".low { background-color: #e5ffe5; }");
        html.AppendLine("</style></head><body>");
        html.AppendLine("<h1>📋 Migration Impact Report</h1>");
        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Type</th><th>Action</th><th>Table</th><th>Name</th><th>Severity</th><th>Reason</th></tr>");

        foreach (var item in impact)
        {
            var severityClass = item.Severity?.ToLowerInvariant() switch
            {
                "high" => "high",
                "medium" => "medium",
                "low" => "low",
                _ => ""
            };

            html.AppendLine($"<tr class='{severityClass}'>" +
                $"<td>{item.Type}</td>" +
                $"<td>{item.Action}</td>" +
                $"<td>{item.Table}</td>" +
                $"<td>{item.Name}</td>" +
                $"<td>{item.Severity ?? "-"}</td>" +
                $"<td>{item.Reason ?? "-"}</td></tr>");
        }

        html.AppendLine("</table>");
        html.AppendLine($"<p><em>Generated on {DateTime.Now:yyyy-MM-dd HH:mm}</em></p>");
        html.AppendLine("</body></html>");

        File.WriteAllText(filePath, html.ToString());
        Console.WriteLine($"📁 HTML report saved to {filePath}");
    }

    private bool IsNewTable(EntityDefinition entity)
    {
        return entity.Columns.Count == 0 &&
               entity.Constraints.Count == 0 &&
               entity.CheckConstraints.Count == 0 &&
               entity.Indexes.Count == 0;
    }

    private void GroupCommands(string title, List<string> commands, string keyword)
    {
        var filtered = commands.Where(c => c.ToUpperInvariant().Contains(keyword.ToUpperInvariant())).ToList();
        if (!filtered.Any()) return;

        Console.WriteLine($"\n{title}: {filtered.Count}");
        foreach (var cmd in filtered)
        {
            var firstLine = cmd.Split('\n').FirstOrDefault()?.Trim();
            Console.WriteLine($"   - {firstLine}");
        }
    }

    /// <summary>
    /// Compares two versions of an entity and returns a list of structural differences,
    /// including added, dropped, and modified columns, constraints, and indexes.
    /// </summary>
    /// <param name="oldEntity">The original entity definition before migration.</param>
    /// <param name="newEntity">The updated entity definition after migration.</param>
    /// <returns>A list of impact items describing the detected changes.</returns>
    public List<ImpactItem> AnalyzeImpact(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        var impact = new List<ImpactItem>();

        // 🔹 Columns
        var oldColumns = oldEntity?.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();
        var newColumns = newEntity?.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();

        foreach (var kvp in newColumns)
        {
            var name = kvp.Key;
            var newCol = kvp.Value;

            if (!oldColumns.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = "Column",
                    Action = "Added",
                    Table = newEntity.Name,
                    Name = name
                });
            }
            else
            {
                var oldCol = oldColumns[name];
                if (oldCol.TypeName != newCol.TypeName || oldCol.IsNullable != newCol.IsNullable)
                {
                    impact.Add(new ImpactItem
                    {
                        Type = "Column",
                        Action = "Modified",
                        Table = newEntity.Name,
                        Name = name,
                        OriginalType = $"{oldCol.TypeName} {(oldCol.IsNullable ? "NULL" : "NOT NULL")}",
                        NewType = $"{newCol.TypeName} {(newCol.IsNullable ? "NULL" : "NOT NULL")}"
                    });
                }
            }
        }

        foreach (var name in oldColumns.Keys)
        {
            if (!newColumns.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = "Column",
                    Action = "Dropped",
                    Table = oldEntity.Name,
                    Name = name
                });
            }
        }

        // 🔹 Check Constraints
        var oldChecks = oldEntity?.CheckConstraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();
        var newChecks = newEntity?.CheckConstraints.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase) ?? new();

        foreach (var kvp in newChecks)
        {
            var name = kvp.Key;
            var newCheck = kvp.Value;

            if (!oldChecks.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = "Constraint",
                    Action = "Added",
                    Table = newEntity.Name,
                    Name = name
                });
            }
            else
            {
                var oldCheck = oldChecks[name];
                if (NormalizeExpression(oldCheck.Expression) != NormalizeExpression(newCheck.Expression))
                {
                    impact.Add(new ImpactItem
                    {
                        Type = "Constraint",
                        Action = "Modified",
                        Table = newEntity.Name,
                        Name = name
                    });
                }
            }
        }

        foreach (var name in oldChecks.Keys)
        {
            if (!newChecks.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = "Constraint",
                    Action = "Dropped",
                    Table = oldEntity.Name,
                    Name = name
                });
            }
        }

        // 🔹 Indexes
        var oldIndexes = oldEntity?.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase) ?? new();
        var newIndexes = newEntity?.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase) ?? new();

        foreach (var kvp in newIndexes)
        {
            var name = kvp.Key;
            var newIndex = kvp.Value;

            if (!oldIndexes.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = "Index",
                    Action = "Added",
                    Table = newEntity.Name,
                    Name = name
                });
            }
            else
            {
                var oldIndex = oldIndexes[name];
                if (!oldIndex.Columns.SequenceEqual(newIndex.Columns, StringComparer.OrdinalIgnoreCase))
                {
                    impact.Add(new ImpactItem
                    {
                        Type = "Index",
                        Action = "Modified",
                        Table = newEntity.Name,
                        Name = name
                    });
                }
            }
        }

        foreach (var name in oldIndexes.Keys)
        {
            if (!newIndexes.ContainsKey(name))
            {
                impact.Add(new ImpactItem
                {
                    Type = "Index",
                    Action = "Dropped",
                    Table = oldEntity.Name,
                    Name = name
                });
            }
        }

        return impact;
    }

    private string NormalizeExpression(string expr)
    {
        return Regex.Replace(expr ?? "", @"\s+", "").ToLowerInvariant();
    }

    private string ExtractName(string cmd)
    {
        var tokens = cmd.Split(new[] { ' ', '\n', '\t', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 2 ? tokens[2] : cmd;
    }

    /// <summary>
    /// Executes a list of SQL commands inside a transaction, with optional interactive mode and detailed logging.
    /// Each executed command is timestamped and stored in an internal log.
    /// </summary>
    /// <param name="commands">List of SQL commands to execute.</param>
    /// <param name="interactive">If true, prompts user before executing each command.</param>
    private void ExecuteCommands(List<string> commands, bool interactive = false)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var executionLog = new List<MigrationLogEntry>();

        try
        {
            Console.WriteLine("🚀 [AutoMigrate] Starting migration...");

            foreach (var cmdText in commands)
            {
                if (string.IsNullOrWhiteSpace(cmdText))
                    continue;

                if (interactive)
                {
                    Console.WriteLine($"\n⚡ Next Command:\n{cmdText}\n");
                    Console.Write("Run this command? [E]xecute / [S]kip / [Q]uit: ");
                    var choice = Console.ReadLine()?.Trim().ToUpperInvariant();
                    if (choice == "S") continue;
                    if (choice == "Q")
                    {
                        Console.WriteLine("🛑 Quitting and rolling back...");
                        transaction.Rollback();
                        return;
                    }
                }

                using var command = new SqlCommand(cmdText, connection, transaction);
                command.ExecuteNonQuery();

                var summary = cmdText.Split('\n')[0].Trim();
                var timestamp = DateTime.Now;

                Console.WriteLine($"✅ [{timestamp:HH:mm:ss}] Executed: {summary}");

                executionLog.Add(new MigrationLogEntry
                {
                    Timestamp = timestamp,
                    Summary = summary,
                    FullCommand = cmdText,
                    Status = "Executed"
                });
            }

            transaction.Commit();
            Console.WriteLine("🎯 [AutoMigrate] Migration committed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Migration failed: {ex.Message}");
            transaction.Rollback();
            Console.WriteLine("↩️ Rolled back all changes.");

            executionLog.Add(new MigrationLogEntry
            {
                Timestamp = DateTime.Now,
                Summary = "[ERROR]",
                FullCommand = ex.Message,
                Status = "Failed"
            });
        }

        // 📝 Log summary
        Console.WriteLine("\n📄 Execution Log:");
        foreach (var entry in executionLog)
            Console.WriteLine($" - [{entry.Timestamp:HH:mm:ss}] {entry.Status}: {entry.Summary}");

        // 🔄 Optional: Save to file or external system
        // SaveLogToFile(executionLog, "migration-log.json");
    }

    /// <summary>
    /// Ensures that the target schema exists in the database.
    /// If not found, creates it dynamically.
    /// </summary>
    /// <param name="schema">The schema name to check and create if missing.</param>
    private void EnsureSchemaExists(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema) || schema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
            return; // dbo always exists

        var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schema}')
BEGIN
    EXEC('CREATE SCHEMA [{schema}]')
END";

        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();

        Console.WriteLine($"✅ [AutoMigrate] Schema [{schema}] ensured.");
    }


    public List<string> SplitSqlCommands(string script)
    {
        return script
            .Replace("\r", "")
            .Split(new[] { "\nGO\n", "\nGO ", "\nGO\r", "\nGO" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(cmd => cmd.Trim())
            .Where(cmd => !string.IsNullOrWhiteSpace(cmd))
            .ToList();
    }

    /// <summary>
    /// Generates a rollback SQL script based on a list of impact items,
    /// reversing added columns, constraints, and indexes, and restoring modified columns.
    /// </summary>
    /// <param name="impact">The list of impact items generated from entity comparison.</param>
    /// <returns>A list of SQL commands that can be used to revert the migration.</returns>
    private List<string> GenerateRollbackScript(List<ImpactItem> impact)
    {
        var rollback = new List<string>();

        foreach (var item in impact)
        {
            switch (item.Type)
            {
                case "Column":
                    if (item.Action == "Added")
                    {
                        rollback.Add($"ALTER TABLE [{item.Table}] DROP COLUMN [{item.Name}];");
                    }
                    else if (item.Action == "Modified" && item.OriginalType != null)
                    {
                        rollback.Add($"ALTER TABLE [{item.Table}] ALTER COLUMN [{item.Name}] {item.OriginalType};");
                    }
                    break;

                case "Constraint":
                    if (item.Action == "Added")
                    {
                        rollback.Add($"ALTER TABLE [{item.Table}] DROP CONSTRAINT [{item.Name}];");
                    }
                    break;

                case "Index":
                    if (item.Action == "Added")
                    {
                        rollback.Add($"DROP INDEX [{item.Name}] ON [{item.Table}];");
                    }
                    break;
            }
        }

        return rollback;
    }

    private (MigrationHistoryStore history, string version) BeginMigrationTracking(string migrationScript, EntityDefinition newEntity)
    {
        var snapshotStore = new JsonSchemaSnapshotStore(@"C:\Snapshots");
        var history = new MigrationHistoryStore(_connectionString, snapshotStore);

        var (isNewVersion, version) = history.EnsureTableAndInsertPending(migrationScript, newEntity);
        if (!isNewVersion)
        {
            Console.WriteLine("[Migration] This migration version already exists. Skipping execution.");
            return (null, null);
        }

        return (history, version);
    }

    private void FinalizeMigrationSuccess(MigrationHistoryStore history, string version, IEnumerable<EntityDefinition> entities, int durationMs)
    {
        history?.MarkApplied(version, durationMs, entities);
    }

    private void FinalizeMigrationFailure(MigrationHistoryStore history, string version, string errorMessage)
    {
        history?.MarkFailed(version, errorMessage);
    }

}