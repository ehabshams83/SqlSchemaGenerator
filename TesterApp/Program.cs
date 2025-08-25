using Microsoft.Data.SqlClient;

using Syn.Core.SqlSchemaGenerator.AttributeHandlers;
using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Migrations;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Scanning;
using Syn.Core.SqlSchemaGenerator.Sql;
using Syn.Core.SqlSchemaGenerator.Storage;

using System.Diagnostics;

using TesterApp.Entities;

namespace TesterApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string server = @".\SqlExpress";
            string databaseName = "SqlSchemaGeneratorTestDb";
            string connectionString =
                $"Server={server};Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=false;TrustServerCertificate=True;";

            EnsureDatabaseExists(server, databaseName);

            //// Handlers
            //var handlers = new List<ISchemaAttributeHandler>
            //{
            //    new IndexAttributeHandler(),
            //    new UniqueAttributeHandler(),
            //    new DefaultValueAttributeHandler(),
            //    new DescriptionAttributeHandler(),
            //    new RequiredAttributeHandler(),
            //    new MaxLengthAttributeHandler(),
            //    new ComputedAttributeHandler(),
            //    new CollationAttributeHandler(),
            //    new CheckConstraintAttributeHandler(),
            //    new IgnoreColumnAttributeHandler(),
            //    new EfCompatibilityAttributeHandler()
            //};

            var builder = new EntityDefinitionBuilder();
            var scanner = new EntityScanner(builder);

            // نحضر snapshot قديم (فارغ أول مرة)
            var oldSnapshot = new List<EntityDefinition>();
            var snapshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snapshot.json");
            var snapshotProvider = new JsonSnapshotProvider(snapshotPath);

            // var snapshotProvider = new InMemorySnapshotProvider(oldSnapshot);

            var stepBuilder = new MigrationStepBuilder();
            var engine = new MigrationEngine(scanner, snapshotProvider, stepBuilder);
            var assembly = typeof(Product).Assembly;

            Console.WriteLine("🚀 Starting full migration demo...\n");

            // قياس الأداء
            var sw = Stopwatch.StartNew();

            // 1️⃣ Scan Entities
            var newEntities = scanner.Scan(assembly).ToList();
            Console.WriteLine($"🔍 Found {newEntities.Count} entities.");

            // 2️⃣ Build Migration Steps
            var steps = stepBuilder.BuildSteps(oldSnapshot, newEntities).ToList();
            Console.WriteLine($"⚙ Steps to execute: {steps.Count}");
            if (!steps.Any())
            {
                Console.WriteLine("✅ No schema changes detected.");
                return;
            }

            // 3️⃣ Generate SQL Scripts
            var generator = new SqlMigrationGenerator();
            var scripts = steps.Select(step =>
            {
                var sql = generator.GenerateSql(step);
                return new SqlMigrationScript
                {
                    EntityName = step.EntityName,
                    Version = "1.0",
                    Sql = sql
                };
            }).ToList();

            Console.WriteLine("\n📜 Generated SQL:");
            foreach (var s in scripts)
            {
                Console.WriteLine($"-- {s.EntityName}");
                Console.WriteLine(s.Sql);
                Console.WriteLine();
            }

            // 4️⃣ Apply Migration Scripts
            var service = new SchemaMigrationService(connectionString);
            var result = await service.ApplyMigrationsAsync(scripts);

            sw.Stop();

            // 5️⃣ Report
            Console.WriteLine("\n📊 Migration Report");
            Console.WriteLine("====================");

            Console.WriteLine("\n✅ Executed:");
            foreach (var s in result.ExecutedScripts)
                Console.WriteLine($" - {s}");

            Console.WriteLine("\n⏭ Skipped:");
            foreach (var s in result.SkippedScripts)
                Console.WriteLine($" - {s}");

            Console.WriteLine($"\n⏱ Total execution time: {sw.ElapsedMilliseconds} ms");

            // 6️⃣ اختبار الأداء بين Sync / Async / Parallel
            Console.WriteLine("\n⚡ Performance Comparison");
            sw.Restart();
            var syncDefs = builder.Build(assembly).ToList();
            sw.Stop();
            Console.WriteLine($"Sync: {syncDefs.Count} entities in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            var asyncDefs = await builder.BuildAsync(assembly);
            sw.Stop();
            Console.WriteLine($"Async: {asyncDefs.Count} entities in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            var parallelDefs = await builder.BuildParallelAsync(assembly);
            sw.Stop();
            Console.WriteLine($"Parallel: {parallelDefs.Count} entities in {sw.ElapsedMilliseconds} ms");
        }

        static void EnsureDatabaseExists(string server, string databaseName)
        {
            using var connection = new SqlConnection(
                $"Server={server};Database=master;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=false;TrustServerCertificate=True;");
            connection.Open();

            using var command = new SqlCommand(
                $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}];",
                connection);
            command.ExecuteNonQuery();

            Console.WriteLine($"💾 Database [{databaseName}] ready to use.");
        }
    }
}