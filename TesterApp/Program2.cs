using Microsoft.Data.SqlClient;

using Syn.Core.SqlSchemaGenerator.AttributeHandlers;
using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Migrations;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Scanning;
using Syn.Core.SqlSchemaGenerator.Sql;
using Syn.Core.SqlSchemaGenerator.Storage;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using TesterApp.Entities;

namespace Syn.Core.SqlSchemaGenerator.ConsoleApp
{
    class Program2
    {
        static async Task Main2(string[] args)
        {

            string server = @".\SqlExpress";
            string databaseName = "SqlSchemaGeneratorTestDb";
            string connectionString = $"Server={server};Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=false;TrustServerCertificate=True;";

            // 🔍 تحقق من وجود قاعدة البيانات أو أنشئها
            EnsureDatabaseExists(server, databaseName);

            var builder = new EntityDefinitionBuilder();
            var scanner = new EntityScanner(builder);

            var oldSnapshot = new List<EntityDefinition>();
            var snapshotProvider = new InMemorySnapshotProvider(oldSnapshot);

            var stepBuilder = new MigrationStepBuilder();
            var engine = new MigrationEngine(scanner, snapshotProvider, stepBuilder);

            var assembly = typeof(Product).Assembly;
            var newEntities = scanner.Scan(assembly);

            var steps = stepBuilder.BuildSteps(oldSnapshot, newEntities);

            if (!steps.Any())
            {
                Console.WriteLine("✅ No Changes");
                return;
            }

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

            var service = new SchemaMigrationService(connectionString);
            var result = await service.ApplyMigrationsAsync(scripts);

            Console.WriteLine("\n📜 Migration Report");
            Console.WriteLine("================================");

            Console.WriteLine("\n✅ Done:");
            foreach (var s in result.ExecutedScripts)
                Console.WriteLine($" - {s}");

            Console.WriteLine("\n⏭ Skip:");
            foreach (var s in result.SkippedScripts)
                Console.WriteLine($" - {s}");
        }

        // 🛠 تحقق من وجود قاعدة البيانات أو إنشاؤها
        static void EnsureDatabaseExists(string server, string databaseName)
        {
            using var connection = new SqlConnection($"Server={server};Database=master;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=false;TrustServerCertificate=True;");
            connection.Open();

            using var command = new SqlCommand(
                $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}];",
                connection);
            command.ExecuteNonQuery();

            Console.WriteLine($"💾 Database [{databaseName}] ready to use.");
        }
    }
}