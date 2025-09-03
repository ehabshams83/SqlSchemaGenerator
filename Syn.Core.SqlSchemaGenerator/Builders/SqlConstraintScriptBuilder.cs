using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Builds SQL scripts for creating and dropping CHECK constraints
    /// from an entity model.
    /// Supports building from either a CLR <see cref="Type"/> or an <see cref="EntityDefinition"/>.
    /// </summary>
    public class SqlConstraintScriptBuilder
    {
        private readonly EntityDefinitionBuilder _entityDefinitionBuilder;
        private readonly DatabaseSchemaReader schemaReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlConstraintScriptBuilder"/> class.
        /// </summary>
        /// <param name="entityDefinitionBuilder">The unified entity definition builder.</param>
        public SqlConstraintScriptBuilder(EntityDefinitionBuilder entityDefinitionBuilder, DatabaseSchemaReader schemaReader)
        {
            _entityDefinitionBuilder = entityDefinitionBuilder
                ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
            this.schemaReader = schemaReader;
        }

        /// <summary>
        /// Generates CREATE CHECK constraint scripts from a CLR type.
        /// </summary>
        public string BuildCreate(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            return BuildCreate(_entityDefinitionBuilder
                .BuildAllWithRelationships(new[] { entityType })
                .First());
        }

        /// <summary>
        /// Generates DROP CHECK constraint scripts for a given entity type.
        /// Uses BuildAllWithRelationships to ensure relationships and indexes are included.
        /// </summary>
        /// <param name="entityType">The CLR type representing the entity.</param>
        /// <returns>SQL script to drop all CHECK constraints for the entity.</returns>
        public string BuildDrop(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));

            // ✅ بناء الكيان الجديد في سياق العلاقات والفهارس
            var entity = _entityDefinitionBuilder
                .BuildAllWithRelationships(new[] { entityType })
                .First();

            return BuildDrop(entity);
        }

        /// <summary>
        /// Generates DROP CHECK constraint scripts from an EntityDefinition.
        /// Adds IF EXISTS safety checks for each constraint.
        /// </summary>
        /// <param name="entity">The entity definition containing CHECK constraints.</param>
        /// <returns>SQL script to drop all CHECK constraints for the entity.</returns>
        public string BuildDrop(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.CheckConstraints == null || !entity.CheckConstraints.Any()) return string.Empty;

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            var sb = new StringBuilder();

            foreach (var c in entity.CheckConstraints)
            {
                sb.AppendLine($@"
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints cc
    WHERE cc.name = N'{EscapeSqlLiteral(c.Name)}'
      AND cc.parent_object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
BEGIN
    ALTER TABLE [{schema}].[{entity.Name}] DROP CONSTRAINT [{c.Name}];
END;
".Trim());
                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }



        /// <summary>
        /// Builds SQL statements to create or update all constraints and indexes for an entity.
        /// Includes CHECK, FOREIGN KEY, UNIQUE, INDEX, and ALTER COLUMN logic with extended descriptions.
        /// </summary>
        /// <param name="entity">The entity definition to generate SQL for.</param>
        /// <returns>Full SQL script for the entity's constraints and indexes.</returns>
        public string BuildCreate(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            var sb = new StringBuilder();

            Console.WriteLine($"[TRACE] In BuildCreate for: {entity.Name}");

            var columnLines = new List<string>();
            var constraintLines = new List<string>();

            // 🔹 الأعمدة
            foreach (var col in entity.Columns)
            {
                var type = col.TypeName;
                var nullable = col.IsNullable ? "NULL" : "NOT NULL";
                var identity = col.IsIdentity ? " IDENTITY(1,1)" : "";
                columnLines.Add($"[{col.Name}] {type}{identity} {nullable}");

                Console.WriteLine($"  🧩 Column: {col.Name} → {type}, Nullable={col.IsNullable}, Identity={col.IsIdentity}");
            }

            // 🔹 قيود CHECK + الفهارس التلقائية
            foreach (var ck in entity.CheckConstraints)
            {
                constraintLines.Add($"CONSTRAINT [{ck.Name}] CHECK ({ck.Expression})");
                Console.WriteLine($"  ✅ Check: {ck.Name} → {ck.Expression}");

                // 🔍 فهرس داعم لو التعبير فيه عمود واضح
                var colName = ExtractColumnFromExpression(ck.Expression);
                if (!string.IsNullOrWhiteSpace(colName))
                {
                    bool alreadyIndexed = entity.Indexes.Any(ix => ix.Columns.Contains(colName));
                    var colDef = entity.Columns.FirstOrDefault(c => c.Name == colName);

                    // ✅ الشرط الجديد: تجاهل الأعمدة من نوع max/text/ntext/image
                    if (colDef != null &&
                        (colDef.TypeName.Contains("max", StringComparison.OrdinalIgnoreCase) ||
                         colDef.TypeName.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                         colDef.TypeName.Contains("ntext", StringComparison.OrdinalIgnoreCase) ||
                         colDef.TypeName.Contains("image", StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine($"    ⚠️ Skipped auto-index for {colName} → type {colDef.TypeName} not indexable");
                        continue;
                    }

                    if (!alreadyIndexed && colDef != null)
                    {
                        var ixName = $"IX_{entity.Name}_{colName}_ForCheck";
                        entity.Indexes.Add(new IndexDefinition
                        {
                            Name = ixName,
                            Columns = new List<string> { colName },
                            IsUnique = false,
                            Description = $"Auto index to support CHECK constraint {ck.Name}"
                        });
                        Console.WriteLine($"    📌 Auto-index added for CHECK: {ixName} on {colName}");
                    }
                }
            }

            // 🔹 المفتاح الأساسي
            if (entity.PrimaryKey?.Columns?.Count > 0)
            {
                var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
                constraintLines.Add($"CONSTRAINT [{entity.PrimaryKey.Name}] PRIMARY KEY ({pkCols})");
                Console.WriteLine($"  🔑 PrimaryKey: {entity.PrimaryKey.Name} → {pkCols}");
            }

            // 🔹 إنشاء الجدول
            var allLines = columnLines.Concat(constraintLines).ToList();
            var tableSql = $@"
CREATE TABLE [{schema}].[{entity.Name}] (
    {string.Join(",\n    ", allLines)}
);";

            sb.AppendLine(tableSql);

            // 🔹 وصف الجدول
            if (!string.IsNullOrWhiteSpace(entity.Description))
            {
                sb.AppendLine($@"
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'{entity.Description}',
    @level0type = N'SCHEMA', @level0name = N'{schema}',
    @level1type = N'TABLE',  @level1name = N'{entity.Name}';");
            }

            // 🔹 وصف الأعمدة
            foreach (var col in entity.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Description)))
            {
                sb.AppendLine($@"
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'{col.Description}',
    @level0type = N'SCHEMA', @level0name = N'{schema}',
    @level1type = N'TABLE',  @level1name = N'{entity.Name}',
    @level2type = N'COLUMN', @level2name = N'{col.Name}';");
            }

            // 🔹 العلاقات (FK)
            foreach (var fk in entity.ForeignKeys)
            {
                var fkName = fk.ConstraintName;
                var referencedColumn = fk.ReferencedColumn ?? "Id";
                var cascadeClause = fk.OnDelete == ReferentialAction.Cascade ? " ON DELETE CASCADE" : "";

                sb.AppendLine($@"
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'{fkName}'
      AND parent_object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
BEGIN
    ALTER TABLE [{schema}].[{entity.Name}]
    DROP CONSTRAINT [{fkName}];
END;

ALTER TABLE [{schema}].[{entity.Name}]
ADD CONSTRAINT [{fkName}]
FOREIGN KEY ([{fk.Column}])
REFERENCES [{schema}].[{fk.ReferencedTable}]([{referencedColumn}]){cascadeClause};");

                Console.WriteLine($"[TRACE:FK] {fkName} → {fk.Column} → {fk.ReferencedTable}.{referencedColumn} Cascade={fk.OnDelete}");
            }
            // 🔹 الفهارس
            foreach (var index in entity.Indexes.DistinctBy(i => i.Name))
            {
                var indexColumns = string.Join(", ", index.Columns.Select(c => $"[{c}]"));
                var includeClause = index.IncludeColumns?.Count > 0
                    ? $" INCLUDE ({string.Join(", ", index.IncludeColumns.Select(c => $"[{c}]"))})"
                    : "";

                var filterClause = !string.IsNullOrWhiteSpace(index.FilterExpression)
                    ? $" WHERE {index.FilterExpression}"
                    : "";

                var traceParts = new List<string>
        {
            $"Name={index.Name}",
            $"Unique={index.IsUnique}",
            $"Columns=[{string.Join(", ", index.Columns)}]"
        };

                if (index.IncludeColumns?.Count > 0)
                    traceParts.Add($"Include=[{string.Join(", ", index.IncludeColumns)}]");

                if (!string.IsNullOrWhiteSpace(index.FilterExpression))
                    traceParts.Add($"Filter=\"{index.FilterExpression}\"");

                if (index.IsFullText)
                    traceParts.Add("FullText=True");

                Console.WriteLine($"[TRACE:Index] {string.Join(", ", traceParts)}");

                if (index.IsFullText)
                {
                    sb.AppendLine($@"
-- 🔍 FULLTEXT INDEX: {index.Name}
CREATE FULLTEXT INDEX ON [{schema}].[{entity.Name}] ({indexColumns})
KEY INDEX [PK_{entity.Name}]
WITH STOPLIST = SYSTEM;");
                    continue;
                }

                var uniqueClause = index.IsUnique ? "UNIQUE " : "";

                sb.AppendLine($@"
IF EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'{index.Name}'
      AND object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
BEGIN
    DROP INDEX [{index.Name}] ON [{schema}].[{entity.Name}];
END;

CREATE {uniqueClause}INDEX [{index.Name}]
ON [{schema}].[{entity.Name}]({indexColumns}){includeClause}{filterClause};");

                if (!string.IsNullOrWhiteSpace(index.Description))
                {
                    sb.AppendLine($@"
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'{index.Description}',
    @level0type = N'SCHEMA',    @level0name = N'{schema}',
    @level1type = N'TABLE',     @level1name = N'{entity.Name}',
    @level2type = N'INDEX',     @level2name = N'{index.Name}';");
                }
            }

            // 🔹 CREATE STATISTICS الذكي
            foreach (var col in entity.Columns)
            {
                bool isNumericOrDate = col.TypeName.StartsWith("int", StringComparison.OrdinalIgnoreCase)
                                    || col.TypeName.StartsWith("decimal", StringComparison.OrdinalIgnoreCase)
                                    || col.TypeName.StartsWith("float", StringComparison.OrdinalIgnoreCase)
                                    || col.TypeName.StartsWith("datetime", StringComparison.OrdinalIgnoreCase);

                bool alreadyIndexed = entity.Indexes.Any(ix => ix.Columns.Contains(col.Name));
                bool usedInCheck = entity.CheckConstraints.Any(ck => ck.Expression.Contains($"[{col.Name}]"));

                if (isNumericOrDate && !alreadyIndexed && usedInCheck)
                {
                    var statName = $"STATS_{entity.Name}_{col.Name}";
                    sb.AppendLine($@"
IF EXISTS (
    SELECT 1 FROM sys.stats WHERE name = N'{statName}'
      AND object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
BEGIN
    DROP STATISTICS [{schema}].[{entity.Name}].[{statName}];
END;

CREATE STATISTICS [{statName}]
ON [{schema}].[{entity.Name}]([{col.Name}]);");

                    Console.WriteLine($"[TRACE:Statistics] Created statistics on {col.Name} → {statName}");
                }
            }

            // 🔹 فهارس الأعمدة المحسوبة
            foreach (var comp in entity.ComputedColumns)
            {
                var indexName = $"IX_{entity.Name}_{comp.Name}_Computed";
                bool alreadyIndexed = entity.Indexes.Any(ix => ix.Columns.Contains(comp.Name));

                if (!alreadyIndexed && IsIndexableExpression(comp.Expression))
                {
                    sb.AppendLine($@"
-- ⚙ Computed column index
CREATE INDEX [{indexName}]
ON [{schema}].[{entity.Name}]([{comp.Name}]);");

                    Console.WriteLine($"[TRACE:ComputedIndex] Created index on computed column {comp.Name} → {indexName}");
                }
            }

            return sb.ToString().Trim();
        }



        // ✅ مساعد لتحديد هل التعبير قابل للفهرسة
        private static bool IsIndexableExpression(string expr)
        {
            var indexableFunctions = new[] { "LEN(", "UPPER(", "LOWER(", "DATEPART(", "YEAR(", "MONTH(", "DAY(" };
            return indexableFunctions.Any(f => expr.Contains(f, StringComparison.OrdinalIgnoreCase));
        }

        // ✅ مساعد لاستخراج اسم العمود من تعبير CHECK بسيط
        private static string? ExtractColumnFromExpression(string expr)
        {
            var match = Regex.Match(expr, @"\[(\w+)\]");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Escapes single quotes for safe inclusion in SQL string literals.
        /// </summary>
        private static string EscapeSqlLiteral(string input) =>
            (input ?? string.Empty).Replace("'", "''");
    }
}