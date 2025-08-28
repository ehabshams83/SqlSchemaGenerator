using Microsoft.Data.SqlClient;

using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator;

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
    private readonly string _connectionString;

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
        bool impactAnalysis = false)
    {
        if (string.IsNullOrWhiteSpace(migrationScript))
        {
            Console.WriteLine("[AutoMigrate] No SQL commands to process.");
            return;
        }

        var schema = newEntity?.Schema ?? "dbo";
        var commands = SplitSqlCommands(migrationScript);

        // 📋 Show Pre-Migration Report
        if (showReport)
        {
            ShowPreMigrationReport(oldEntity, newEntity, commands, impactAnalysis);
            Console.WriteLine();
        }

        // ⚡ AutoMerge Mode
        if (autoMerge)
        {
            Console.WriteLine("⚡ [AutoMigrate] Auto Merge mode: Checking if all changes are safe...");

            // ✅ تمرير oldEntity و newEntity للتحليل
            var safety = AnalyzeMigrationSafety(commands, oldEntity, newEntity);

            if (safety.IsSafe)
            {
                Console.WriteLine("✅ All changes are safe. Executing automatically...");
                EnsureSchemaExists(schema);
                Console.WriteLine("📜 Migration Script Preview:");
                foreach (var cmd in commands)
                    Console.WriteLine(cmd + "\nGO\n");
                ExecuteCommands(commands);
                return;
            }
            else
            {
                Console.WriteLine("⚠️ Risky changes detected:");
                foreach (var reason in safety.Reasons)
                    Console.WriteLine($"   - {reason}");

                previewOnly = true;
            }
        }



        // 👀 Preview Mode
        if (previewOnly)
        {
            Console.Write("Do you want to proceed with migration? (Y/N): ");
            var proceed = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (proceed != "Y")
            {
                Console.WriteLine("❌ [AutoMigrate] Migration cancelled.");
                return;
            }
        }

        // 🔍 Dry Run Mode
        if (dryRun)
        {
            Console.WriteLine("🔍 [AutoMigrate] Dry Run mode: No changes will be applied.");
            foreach (var cmd in commands)
                Console.WriteLine($"-- [DryRun] Would execute:\n{cmd}\n");
            Console.WriteLine("✅ [AutoMigrate] Dry Run completed.");
            return;
        }

        // ✅ Ensure schema exists before executing
        EnsureSchemaExists(schema);

        Console.WriteLine("📜 Migration Script Preview:");
        foreach (var cmd in commands)
            Console.WriteLine(cmd + "\nGO\n");

        // 🚀 Execute Commands
        ExecuteCommands(commands, interactive);
    }


    /// <summary>
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
                var keyName = !string.IsNullOrWhiteSpace(drop.Name) ? drop.Name : (drop.ReferencedColumns?.FirstOrDefault() ?? "");
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
    /// Displays a detailed report of migration changes, including impact analysis.
    /// </summary>
    private void ShowPreMigrationReport(EntityDefinition oldEntity, EntityDefinition newEntity, List<string> commands, bool impactAnalysis)
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

        if (impactAnalysis)
        {
            Console.WriteLine("\n⚠️ Impact Analysis Warnings:");
            AnalyzeImpact(commands);
        }

        Console.WriteLine("===================================");
        Console.WriteLine($"Total commands: {commands.Count}");
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

    private void AnalyzeImpact(List<string> commands)
    {
        foreach (var cmd in commands)
        {
            var upper = cmd.ToUpperInvariant();
            if (upper.Contains("DROP COLUMN"))
                Console.WriteLine($"   - {ExtractName(cmd)}: Dropping this column may lead to data loss.");
            if (upper.Contains("DROP CONSTRAINT") && upper.Contains("FK_"))
                Console.WriteLine($"   - {ExtractName(cmd)}: Dropping this FK may break relational integrity.");
            if (upper.Contains("ADD CONSTRAINT") && upper.Contains("FOREIGN KEY"))
                Console.WriteLine($"   - {ExtractName(cmd)}: Adding a FK may fail if existing data violates the relationship.");
            if (upper.Contains("ALTER COLUMN") && upper.Contains("NOT NULL"))
                Console.WriteLine($"   - {ExtractName(cmd)}: Changing to NOT NULL may fail if NULL values exist.");
        }
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
    /// Analyzes a list of SQL commands and determines whether the migration is safe.
    /// A safe migration contains only additive operations (e.g., ADD COLUMN, ADD CONSTRAINT).
    /// Returns false if any risky operation is detected, and prints reasons.
    /// </summary>
    /// <param name="commands">List of SQL commands to analyze.</param>
    /// <returns>True if all commands are safe; otherwise false.</returns>
    private bool IsSafeMigration(List<string> commands)
    {
        bool isSafe = true;

        foreach (var cmd in commands)
        {
            var upper = cmd.ToUpperInvariant();
            var summary = cmd.Split('\n')[0].Trim();

            if (upper.Contains("DROP COLUMN"))
            {
                Console.WriteLine($"❌ Unsafe: Dropping column → {summary}");
                isSafe = false;
            }
            else if (upper.Contains("DROP CONSTRAINT"))
            {
                Console.WriteLine($"❌ Unsafe: Dropping constraint → {summary}");
                isSafe = false;
            }
            else if (upper.Contains("ALTER COLUMN"))
            {
                Console.WriteLine($"❌ Unsafe: Altering column → {summary}");
                isSafe = false;
            }
            else if (upper.Contains("DROP INDEX"))
            {
                Console.WriteLine($"❌ Unsafe: Dropping index → {summary}");
                isSafe = false;
            }
            else if (upper.Contains("ALTER TABLE") && upper.Contains("DROP"))
            {
                Console.WriteLine($"❌ Unsafe: ALTER TABLE with DROP → {summary}");
                isSafe = false;
            }
            else
            {
                Console.WriteLine($"✅ Safe: {summary}");
            }
        }

        return isSafe;
    }
}