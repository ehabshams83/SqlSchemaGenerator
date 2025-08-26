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




        private static void RunMigration(DbConnection connection, IEnumerable<Assembly> assemblies)
        {
            var entityDefBuilder = new EntityDefinitionBuilder();
            var schemaReader = new DatabaseSchemaReader(connection);
            var service = new EntityDefinitionService(entityDefBuilder, schemaReader);

            var tableBuilder = new SqlTableScriptBuilder(entityDefBuilder);
            var constraintBuilder = new SqlConstraintScriptBuilder(entityDefBuilder, schemaReader);

            var sb = new StringBuilder();

            // ✅ المرحلة الأساسية: بناء الكيانات وتحليل العلاقات والقيود
            var entityTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract);

            // ⬅ هنا التعديل المهم: استخدام BuildAllWithRelationships
            var builtEntities = entityDefBuilder.BuildAllWithRelationships(entityTypes).ToList();

            // إثراء الكيانات (إضافة معلومات من قاعدة البيانات لو موجودة)
            var enrichedEntities = builtEntities.Select(e =>
            {
                if (e.ClrType == null) return e;

                var enriched = service.BuildFull(e.ClrType);
                CopyRelationshipsAndConstraints(e, enriched);

                // 🆕 تطبيق الـ PK override بعد أي Enrich
                EntityDefinitionBuilder.ApplyPrimaryKeyOverrides(enriched);

                return enriched;
            }).ToList();

            // ترتيب الكيانات حسب الاعتمادية
            var sortedEntities = EntityDefinitionBuilder.SortEntitiesByDependency(enrichedEntities);

            // عرض مخطط العلاقات
            PrintRelationshipGraph(sortedEntities);

            // 🛠 تتبّع قبل الدخول لمرحلة BuildCreate
            foreach (var entity in sortedEntities)
            {
                Console.WriteLine($"[TRACE] Passing entity to BuildCreate: {entity.Name}");
                Console.WriteLine($"  Relationships: {entity.Relationships.Count}");
                foreach (var rel in entity.Relationships)
                    Console.WriteLine($"    🔗 {rel.SourceEntity} {rel.Type} -> {rel.TargetEntity} (Cascade={rel.OnDelete})");

                Console.WriteLine($"  CheckConstraints: {entity.CheckConstraints.Count}");
                foreach (var ck in entity.CheckConstraints)
                    Console.WriteLine($"    ✅ {ck.Name}: {ck.Expression}");

                Console.WriteLine($"  ForeignKeys: {entity.ForeignKeys.Count}");

                var oldEntity = schemaReader.GetEntityDefinition(entity.Schema, entity.Name);

                if (oldEntity == null)
                {
                    AppendIfNotEmpty(sb, tableBuilder.Build(entity));
                }

                AppendIfNotEmpty(sb, constraintBuilder.BuildCreate(entity));
            }

            var finalSql = sb.ToString();
            if (!string.IsNullOrWhiteSpace(finalSql))
            {
                Console.WriteLine("📜 Final SQL Script:");
                Console.WriteLine(new string('-', 40));
                Console.WriteLine(finalSql);
                Console.WriteLine(new string('-', 40));

                // للتنفيذ الفعلي على قاعدة البيانات:
                // using var cmd = connection.CreateCommand();
                // cmd.CommandText = finalSql;
                // cmd.ExecuteNonQuery();
            }
        }



        ///// <summary>
        ///// Executes schema migration for a set of assemblies by generating and executing
        ///// SQL scripts to create or alter tables, indexes, and constraints.
        ///// Automatically builds entities, infers relationships, fixes missing keys,
        ///// and respects dependency order between related tables.
        ///// </summary>
        ///// <param name="connection">Active database connection.</param>
        ///// <param name="assemblies">Assemblies containing entity types to migrate.</param>
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

        //    var entityTypes = assemblies
        //        .SelectMany(a => a.GetTypes())
        //        .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract);

        //    var builtEntities = entityDefBuilder.BuildAllWithRelationships(entityTypes);

        //    // ✅ دمج العلاقات والقيود بعد إثراء الكيانات
        //    var enrichedEntities = builtEntities.Select(e =>
        //    {
        //        if (e.ClrType == null)
        //            return e; // كيان مولّد تلقائيًا، لا تعيد بناءه

        //        var enriched = service.BuildFull(e.ClrType);
        //        CopyRelationshipsAndConstraints(e, enriched);
        //        return enriched;
        //    }).ToList();

        //    var sortedEntities = EntityDefinitionBuilder.SortEntitiesByDependency(enrichedEntities);

        //    // ✅ طباعة العلاقات قبل الترحيل
        //    PrintRelationshipGraph(sortedEntities);

        //    var definedEntities = sortedEntities.Where(e => e.ClrType != null).ToList();
        //    var generatedEntities = sortedEntities.Where(e => e.ClrType == null).ToList();

        //    foreach (var entity in definedEntities.Concat(generatedEntities))
        //    {
        //        foreach (var rel in entity.Relationships)
        //        {
        //            var target = sortedEntities.FirstOrDefault(e =>
        //                string.Equals(e.Name, rel.TargetEntity, StringComparison.OrdinalIgnoreCase));

        //            if (target == null)
        //                throw new InvalidOperationException($"Entity '{entity.Name}' has relationship to missing entity '{rel.TargetEntity}'.");

        //            if (target.PrimaryKey == null || !target.PrimaryKey.Columns.Any())
        //            {
        //                target.PrimaryKey = new PrimaryKeyDefinition
        //                {
        //                    Columns = new List<string> { "Id" },
        //                    Name = $"PK_{target.Name}"
        //                };

        //                if (!target.Columns.Any(c => c.Name == "Id"))
        //                {
        //                    target.Columns.Add(new ColumnDefinition
        //                    {
        //                        Name = "Id",
        //                        TypeName = "int",
        //                        IsNullable = false
        //                    });
        //                }

        //                Console.WriteLine($"[AutoFix] Added primary key 'Id' to referenced entity '{target.Name}'");
        //            }


        //            if (rel.Type == RelationshipType.OneToOne)
        //            {
        //                var fkColumn = $"{entity.Name}Id";
        //                if (!target.Constraints.Any(c => c.Type == ConstraintType.Unique.ToString() && c.Columns.Contains(fkColumn)))
        //                {
        //                    target.Constraints.Add(new ConstraintDefinition
        //                    {
        //                        Type = ConstraintType.Unique.ToString(),
        //                        Columns = new List<string> { fkColumn },
        //                        Name = $"UQ_{target.Name}_{fkColumn}"
        //                    });

        //                    Console.WriteLine($"[AutoFix] Added unique constraint for One-to-One between '{entity.Name}' and '{target.Name}'");
        //                }
        //            }
        //        }

        //        var oldEntity = schemaReader.GetEntityDefinition(entity.Schema, entity.Name);

        //        if (oldEntity == null)
        //        {
        //            AppendIfNotEmpty(sb, tableBuilder.Build(entity));
        //            AppendIfNotEmpty(sb, indexBuilder.BuildCreate(entity));
        //            AppendIfNotEmpty(sb, constraintBuilder.BuildCreate(entity));
        //        }
        //        else
        //        {
        //            AppendIfNotEmpty(sb, alterBuilder.Build(oldEntity, entity));
        //        }
        //    }

        //    var finalSql = sb.ToString();
        //    if (!string.IsNullOrWhiteSpace(finalSql))
        //    {
        //        using var cmd = connection.CreateCommand();
        //        cmd.CommandText = finalSql;
        //        //cmd.ExecuteNonQuery();

        //        Console.WriteLine("📜 Final SQL Script:");
        //        Console.WriteLine(new string('-', 40));
        //        Console.WriteLine(finalSql);
        //        Console.WriteLine(new string('-', 40));

        //    }
        //}





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
        /// Copies relationships, constraints, foreign keys, and primary key
        /// from a source entity to a target entity, preserving inferred metadata.
        /// </summary>
        private static void CopyRelationshipsAndConstraints(EntityDefinition source, EntityDefinition target)
        {
            target.Relationships = source.Relationships;
            target.Constraints ??= new List<ConstraintDefinition>();
            target.ForeignKeys ??= new List<ForeignKeyDefinition>();

            foreach (var fk in source.ForeignKeys)
                if (!target.ForeignKeys.Any(x => x.Column == fk.Column))
                    target.ForeignKeys.Add(fk);

            foreach (var constraint in source.Constraints)
                if (!target.Constraints.Any(x => x.Name == constraint.Name))
                    target.Constraints.Add(constraint);

            if (target.PrimaryKey == null && source.PrimaryKey != null)
                target.PrimaryKey = source.PrimaryKey;
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
                        RelationshipType.OneToOne => "───1:1───>",
                        RelationshipType.OneToMany => "───1:N───>",
                        RelationshipType.ManyToOne => "───N:1───>",
                        RelationshipType.ManyToMany => "───N:N───>",
                        _ => "──────>"
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