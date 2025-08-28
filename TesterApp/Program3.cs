using Syn.Core.SqlSchemaGenerator.AttributeHandlers;
using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using TesterApp.Models.MTM;

namespace TesterApp
{
    class Program3
    {
        static async Task Main3(string[] args)
        {
            var handlers = new List<ISchemaAttributeHandler>
            {
                new IndexAttributeHandler(),
                //new UniqueAttributeHandler(),
                new DefaultValueAttributeHandler(),
                new DescriptionAttributeHandler(),
                new RequiredAttributeHandler(),
                new MaxLengthAttributeHandler(),
                new ComputedAttributeHandler(),
                new CollationAttributeHandler(),
                new CheckConstraintAttributeHandler(),
                new IgnoreColumnAttributeHandler(),
                new EfCompatibilityAttributeHandler()
            };

            var builder = new EntityDefinitionBuilder(handlers);
            var assembly = typeof(Product).Assembly;

            Console.WriteLine("🚀 Starting Console Demo...\n");

            // 1️⃣ Synchronous
            var sw = Stopwatch.StartNew();
            var syncDefs = builder.Build(assembly).ToList();
            sw.Stop();
            Console.WriteLine($"🔹 Sync: Built {syncDefs.Count} entities in {sw.ElapsedMilliseconds} ms");

            // 2️⃣ Async
            sw.Restart();
            var asyncDefs = await builder.BuildAsync(assembly);
            sw.Stop();
            Console.WriteLine($"🔹 Async: Built {asyncDefs.Count} entities in {sw.ElapsedMilliseconds} ms");

            // 3️⃣ Parallel Async
            sw.Restart();
            var parallelDefs = await builder.BuildParallelAsync(assembly);
            sw.Stop();
            Console.WriteLine($"🔹 Parallel Async: Built {parallelDefs.Count} entities in {sw.ElapsedMilliseconds} ms");

            // 🖨 Print sample SQL (لو فيه جينيريتر عندك)
            Console.WriteLine("\n📜 Example Generated SQL (Fake example for demo):");
            foreach (var def in parallelDefs)
            {
                Console.WriteLine($"-- {def.Name}");
                Console.WriteLine($"CREATE TABLE [{def.Schema}].[{def.Name}] (...)\n");
            }

            Console.WriteLine("\n✅ Demo Finished.");
        }
    }
}