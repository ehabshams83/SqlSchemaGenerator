using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;
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
    public static class MigrationRunner
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
        /// Executes schema migration for a set of assemblies by generating and executing
        /// SQL scripts to create or alter tables, indexes, and constraints.
        /// Automatically builds entities, infers relationships, fixes missing keys,
        /// and respects dependency order between related tables.
        /// </summary>
        /// <param name="connection">Active database connection.</param>
        /// <param name="assemblies">Assemblies containing entity types to migrate.</param>
        private static void RunMigration(DbConnection connection, IEnumerable<Assembly> assemblies)
        {
            // === 1) إعداد أدوات البناء والخدمة ===
            var entityDefBuilder = new EntityDefinitionBuilder();
            var schemaReader = new DatabaseSchemaReader(connection);
            var service = new EntityDefinitionService(entityDefBuilder, schemaReader);

            var tableBuilder = new SqlTableScriptBuilder(entityDefBuilder);
            var indexBuilder = new SqlIndexScriptBuilder(entityDefBuilder);
            var constraintBuilder = new SqlConstraintScriptBuilder(entityDefBuilder);
            var alterBuilder = new SqlAlterTableBuilder(entityDefBuilder);

            var sb = new StringBuilder();

            // === 2) استخراج أنواع الكيانات من الـ Assemblies ===
            var entityTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract);

            // === 3) بناء الكيانات + استنتاج العلاقات وتسجيلها
            var builtEntities = entityDefBuilder.BuildAllWithRelationships(entityTypes);

            // === 4) إثراء الكيانات ببيانات القيود من قاعدة البيانات
            var enrichedEntities = builtEntities
                .Select(e => service.BuildFull(e.ClrType ?? e.GetType()))
                .ToList();

            // === 5) ترتيب الكيانات حسب العلاقات الخارجية (FKs)
            var sortedEntities = EntityDefinitionBuilder.SortEntitiesByDependency(enrichedEntities);

                        PrintRelationshipGraph(sortedEntities);
            // === 6) تقسيم الكيانات إلى معرفّة ومولّدة تلقائيًا
            var definedEntities = sortedEntities.Where(e => e.ClrType != null).ToList();
            var generatedEntities = sortedEntities.Where(e => e.ClrType == null).ToList();

            // === 7) تنفيذ الترحيل: المعرّفة أولًا، ثم التلقائية
            foreach (var entity in definedEntities.Concat(generatedEntities))
            {
                // 🔐 تحقق من صحة العلاقات المسجلة
                foreach (var rel in entity.Relationships)
                {
                    var target = sortedEntities.FirstOrDefault(e =>
                        string.Equals(e.Name, rel.TargetEntity, StringComparison.OrdinalIgnoreCase));

                    if (target == null)
                    {
                        throw new InvalidOperationException(
                            $"Entity '{entity.Name}' has relationship to missing entity '{rel.TargetEntity}'."
                        );
                    }

                    // ✅ إصلاح تلقائي: إضافة مفتاح أساسي للطرف المرتبط لو مفقود
                    if (target.PrimaryKey == null || !target.PrimaryKey.Columns.Any())
                    {
                        target.PrimaryKey = new PrimaryKeyDefinition
                        {
                            Columns = new List<string> { "Id" },
                            Name = $"PK_{target.Name}"
                        };

                        target.Columns.Add(new ColumnDefinition
                        {
                            Name = "Id",
                            TypeName = "int",
                            IsNullable = false
                        });

                        Console.WriteLine($"[AutoFix] Added primary key 'Id' to referenced entity '{target.Name}'");
                    }

                    // ✅ توليد قيد فريد لعلاقة One-to-One لو مفقود
                    if (rel.Type == RelationshipType.OneToOne)
                    {
                        var fkColumn = $"{entity.Name}Id";
                        if (!target.Constraints.Any(c => c.Type == ConstraintType.Unique.ToString() && c.Columns.Contains(fkColumn)))
                        {
                            target.Constraints.Add(new ConstraintDefinition
                            {
                                Type = ConstraintType.Unique.ToString(),
                                Columns = new List<string> { fkColumn },
                                Name = $"UQ_{target.Name}_{fkColumn}"
                            });

                            Console.WriteLine($"[AutoFix] Added unique constraint for One-to-One between '{entity.Name}' and '{target.Name}'");
                        }
                    }
                }

                // 🔄 مقارنة مع المخطط الحالي
                var oldEntity = schemaReader.GetEntityDefinition(entity.Schema, entity.Name);

                if (oldEntity == null)
                {
                    AppendIfNotEmpty(sb, tableBuilder.Build(entity));
                    AppendIfNotEmpty(sb, indexBuilder.BuildCreate(entity));
                    AppendIfNotEmpty(sb, constraintBuilder.BuildCreate(entity));
                }
                else
                {
                    AppendIfNotEmpty(sb, alterBuilder.Build(oldEntity, entity));
                }
            }

            // === 8) تنفيذ السكريبت النهائي لو فيه تغييرات
            var finalSql = sb.ToString();
            if (!string.IsNullOrWhiteSpace(finalSql))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = finalSql;
                cmd.ExecuteNonQuery();
            }
        }

        ///// Executes the core migration process by:
        ///// <list type="number">
        ///// <item><description>Scanning CLR entity types from provided assemblies.</description></item>
        ///// <item><description>Comparing them to the existing database schema.</description></item>
        ///// <item><description>Generating CREATE or ALTER statements based on differences.</description></item>
        ///// <item><description>Executing the generated SQL directly against the database connection.</description></item>
        ///// </list>
        ///// </summary>
        ///// <param name="connection">The open database connection to use for schema inspection and execution.</param>
        ///// <param name="assemblies">The assemblies containing the entity type definitions to process.</param>
        //private static void RunMigration(DbConnection connection, IEnumerable<Assembly> assemblies)
        //{
        //    var entityDefBuilder = new EntityDefinitionBuilder();
        //    var schemaReader = new DatabaseSchemaReader(connection);
        //    var service = new EntityDefinitionService(entityDefBuilder, schemaReader);

        //    var tableBuilder = new SqlTableScriptBuilder(entityDefBuilder);
        //    var indexBuilder = new SqlIndexScriptBuilder(entityDefBuilder);
        //    var constraintBuilder = new SqlConstraintScriptBuilder(entityDefBuilder);
        //    var alterBuilder = new SqlAlterTableBuilder(entityDefBuilder);

        //    var sb = new StringBuilder();

        //    foreach (var assembly in assemblies)
        //    {
        //        var entities = assembly.GetTypes()
        //            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
        //            .Select(t => new
        //            {
        //                Type = t,
        //                NewEntity = service.BuildFull(t) // الكيان الجديد من الكود + DB
        //            })
        //            .ToList();

        //        foreach (var item in entities)
        //        {
        //            // بدل ما ناخد نسخة ناقصة من الـ DB بس،
        //            // هنحاول نبني الـ OldEntity كامل بنفس الخدمة
        //            var oldEntity = schemaReader.GetEntityDefinition(item.NewEntity.Schema, item.NewEntity.Name);

        //            if (oldEntity == null)
        //            {
        //                AppendIfNotEmpty(sb, tableBuilder.Build(item.NewEntity));
        //                AppendIfNotEmpty(sb, indexBuilder.BuildCreate(item.NewEntity));
        //                AppendIfNotEmpty(sb, constraintBuilder.BuildCreate(item.NewEntity));
        //            }
        //            else
        //            {
        //                // نغني الـ OldEntity من الـ DB بقراءة القيود والتفاصيل
        //                service.BuildFull(oldEntity.GetType()); // أو استدعاء enrichment مباشر إذا النوع متوفر

        //                AppendIfNotEmpty(sb, alterBuilder.Build(oldEntity, item.NewEntity));
        //            }
        //        }
        //    }

        //    var finalSql = sb.ToString();
        //    if (!string.IsNullOrWhiteSpace(finalSql))
        //    {
        //        using var cmd = connection.CreateCommand();
        //        cmd.CommandText = finalSql;
        //        cmd.ExecuteNonQuery();
        //    }
        //}


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


        /// <summary>
        /// Prints a textual graph of all relationships between entities.
        /// Shows relationship type, source and target entities, and join table if applicable.
        /// </summary>
        /// <param name="entities">List of <see cref="EntityDefinition"/> objects with relationships.</param>
        public static void PrintRelationshipGraph(IEnumerable<EntityDefinition> entities)
        {
            Console.WriteLine("📊 Relationship Graph:");
            Console.WriteLine(new string('-', 40));

            Console.WriteLine("🔍 Reviewing entity relationships before migration...");

            foreach (var entity in entities)
            {
                foreach (var rel in entity.Relationships)
                {
                    string arrow = rel.Type switch
                    {
                        RelationshipType.OneToOne => "───1:1───▶",
                        RelationshipType.OneToMany => "───1:N───▶",
                        RelationshipType.ManyToOne => "───N:1───▶",
                        RelationshipType.ManyToMany => "───N:N───▶",
                        _ => "──────▶"
                    };

                    string joinInfo = rel.Type == RelationshipType.ManyToMany
                        ? $" [JoinTable: {(rel.IsExplicitJoinEntity ? "Explicit" : "Auto")} '{rel.JoinEntityName}']"
                        : "";

                    Console.WriteLine($"{rel.SourceEntity} {arrow} {rel.TargetEntity}{joinInfo}");
                }
            }

            Console.WriteLine(new string('-', 40));
        }
    }
}