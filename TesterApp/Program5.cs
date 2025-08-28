using Microsoft.Data.SqlClient;

using Syn.Core.SqlSchemaGenerator;
using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;

using TesterApp.Models.MTM;

namespace TesterApp;

class Program5
{
    static void Main5(string[] args)
    {
        string server = @".\SqlExpress";
        string databaseName = "SqlSchemaGeneratorTestDb";
        string connectionString =
            $"Server={server};Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=false;TrustServerCertificate=True;";


        var assemblies = new[] { typeof(Product).Assembly }; // أو Assembly.LoadFrom(...)

        // ✅ طباعة العلاقات قبل الترحيل
        var entityDefBuilder = new EntityDefinitionBuilder();
        var entityTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract);

        //var entities = entityDefBuilder.BuildAllWithRelationships(entityTypes);
        


        //RelationshipPrinter.PrintRelationshipGraph(entities);

        // ✅ تنفيذ الترحيل
        //MigrationRunner.AutoMigrate(connectionString, assemblies);

        Console.WriteLine("✅ Migration completed.");
    }
}
