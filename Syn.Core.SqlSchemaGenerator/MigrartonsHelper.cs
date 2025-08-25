using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Services;

using System.Data.Common;
using System.Reflection;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator
{
    /// <summary>
    /// Provides extension methods for automatically migrating SQL Server schema definitions
    /// based on current CLR entity models, with support for both EF Core DbContext
    /// and direct SQL connections.
    /// </summary>
    public static class AutoMigrationExtensions
    {
        /// <summary>
        /// Performs automatic schema migration for all specified assemblies
        /// using the provided <see cref="DbContext"/> connection.
        /// </summary>
        /// <param name="context">
        /// The EF Core <see cref="DbContext"/> whose database connection will be used.
        /// </param>
        /// <param name="assemblies">
        /// One or more assemblies containing CLR entity types to scan for schema generation.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="assemblies"/> is null or empty.
        /// </exception>
        public static void AutoMigrate(this DbContext context, IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null || !assemblies.Any())
                throw new ArgumentException("You must provide at least one Assembly.", nameof(assemblies));

            var connectionString = context.Database.GetDbConnection().ConnectionString;
            EnsureDatabaseExists(connectionString);

            using var connection = context.Database.GetDbConnection();
            connection.Open();
            RunMigration(connection, assemblies);
        }

        /// <summary>
        /// Performs automatic schema migration for all specified assemblies
        /// using a direct SQL Server connection string.
        /// </summary>
        /// <param name="connectionString">
        /// A valid SQL Server connection string for the target database.
        /// </param>
        /// <param name="assemblies">
        /// One or more assemblies containing CLR entity types to scan for schema generation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="connectionString"/> is null or whitespace.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="assemblies"/> is null or empty.
        /// </exception>
        public static void AutoMigrate(string connectionString, IEnumerable<Assembly> assemblies)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (assemblies == null || !assemblies.Any())
                throw new ArgumentException("You must provide at least one Assembly.", nameof(assemblies));

            EnsureDatabaseExists(connectionString);
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            RunMigration(connection, assemblies);
        }

        /// <summary>
        /// Executes the core migration process by:
        /// <list type="number">
        /// <item><description>Scanning CLR entity types from provided assemblies.</description></item>
        /// <item><description>Comparing them to the existing database schema.</description></item>
        /// <item><description>Generating CREATE or ALTER statements based on differences.</description></item>
        /// <item><description>Executing the generated SQL directly against the database connection.</description></item>
        /// </list>
        /// </summary>
        /// <param name="connection">The open database connection to use for schema inspection and execution.</param>
        /// <param name="assemblies">The assemblies containing the entity type definitions to process.</param>
        private static void RunMigration(DbConnection connection, IEnumerable<Assembly> assemblies)
        {
            var entityDefBuilder = new EntityDefinitionBuilder();
            var schemaReader = new DatabaseSchemaReader(connection);
            var service = new EntityDefinitionService(entityDefBuilder, schemaReader);

            var tableBuilder = new SqlTableScriptBuilder(entityDefBuilder);
            var indexBuilder = new SqlIndexScriptBuilder(entityDefBuilder);
            var constraintBuilder = new SqlConstraintScriptBuilder(entityDefBuilder);
            var alterBuilder = new SqlAlterTableBuilder(entityDefBuilder);

            var sb = new StringBuilder();

            foreach (var assembly in assemblies)
            {
                var entities = assembly.GetTypes()
                    .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
                    .Select(t => new
                    {
                        Type = t,
                        NewEntity = service.BuildFull(t) // الكيان الجديد من الكود + DB
                    })
                    .ToList();

                foreach (var item in entities)
                {
                    // بدل ما ناخد نسخة ناقصة من الـ DB بس،
                    // هنحاول نبني الـ OldEntity كامل بنفس الخدمة
                    var oldEntity = schemaReader.GetEntityDefinition(item.NewEntity.Schema, item.NewEntity.Name);

                    if (oldEntity == null)
                    {
                        AppendIfNotEmpty(sb, tableBuilder.Build(item.NewEntity));
                        AppendIfNotEmpty(sb, indexBuilder.BuildCreate(item.NewEntity));
                        AppendIfNotEmpty(sb, constraintBuilder.BuildCreate(item.NewEntity));
                    }
                    else
                    {
                        // نغني الـ OldEntity من الـ DB بقراءة القيود والتفاصيل
                        service.BuildFull(oldEntity.GetType()); // أو استدعاء enrichment مباشر إذا النوع متوفر

                        AppendIfNotEmpty(sb, alterBuilder.Build(oldEntity, item.NewEntity));
                    }
                }
            }

            var finalSql = sb.ToString();
            if (!string.IsNullOrWhiteSpace(finalSql))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = finalSql;
                cmd.ExecuteNonQuery();
            }
        }


        /// <summary>
        /// Ensures that the target database exists. If it does not, this method
        /// will create it automatically using the provided connection string.
        /// </summary>
        /// <param name="connectionString">
        /// A valid SQL Server connection string pointing to the target database.
        /// </param>
        /// <returns>
        /// True if the database was created during this call, false if it already existed.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="connectionString"/> is null or whitespace.
        /// </exception>
        private static bool EnsureDatabaseExists(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;

            // نعدل الاتصال ليشير إلى master بدلاً من قاعدة البيانات الهدف
            builder.InitialCatalog = "master";
            var masterConnectionString = builder.ToString();

            using var connection = new SqlConnection(masterConnectionString);
            connection.Open();

            // تحقق هل قاعدة البيانات موجودة
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"SELECT db_id(@dbName)";
            checkCmd.Parameters.AddWithValue("@dbName", databaseName);

            var exists = checkCmd.ExecuteScalar() != DBNull.Value && checkCmd.ExecuteScalar() != null;
            if (exists)
                return false; // قاعدة البيانات موجودة بالفعل

            // إنشاء قاعدة البيانات
            var createCmd = connection.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE [{databaseName}]";
            createCmd.ExecuteNonQuery();

            return true; // تم الإنشاء الآن
        }

        /// <summary>
        /// Appends a SQL fragment to the provided <see cref="StringBuilder"/>
        /// only if the fragment is not null, empty, or whitespace.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
        /// <param name="sql">The SQL string to append if valid.</param>
        private static void AppendIfNotEmpty(StringBuilder sb, string sql)
        {
            if (!string.IsNullOrWhiteSpace(sql))
                sb.AppendLine(sql);
        }
    }
}