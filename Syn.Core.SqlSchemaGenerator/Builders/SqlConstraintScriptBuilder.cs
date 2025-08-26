using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Linq;
using System.Text;

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
            return BuildCreate(_entityDefinitionBuilder.Build(entityType));
        }

        /// <summary>
        /// Generates DROP CHECK constraint scripts from a CLR type.
        /// </summary>
        public string BuildDrop(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            return BuildDrop(_entityDefinitionBuilder.Build(entityType));
        }



        /// <summary>
        /// Builds SQL ALTER TABLE statements to create all constraints for an entity.
        /// Includes CHECK, FOREIGN KEY, and UNIQUE constraints, with optional extended descriptions.
        /// </summary>
        /// <summary>
        /// Builds SQL ALTER TABLE statements to create all constraints for an entity.
        /// Includes CHECK, FOREIGN KEY, and UNIQUE constraints, with optional extended descriptions.
        /// </summary>
        /// <summary>
        /// Builds SQL ALTER TABLE statements to create all constraints for an entity.
        /// Includes CHECK, FOREIGN KEY, and UNIQUE constraints, with optional extended descriptions.
        /// </summary>
        /// <summary>
        /// Builds SQL ALTER TABLE statements to create all constraints for an entity.
        /// Includes CHECK, FOREIGN KEY, and UNIQUE constraints, with optional extended descriptions.
        /// </summary>
        /// <summary>
        /// Builds SQL statements to create or update all constraints and indexes for an entity.
        /// Includes CHECK, FOREIGN KEY, UNIQUE, INDEX, and ALTER COLUMN logic with extended descriptions.
        /// </summary>
        /// <param name="entity">The entity definition to generate SQL for.</param>
        /// <returns>Full SQL script for the entity's constraints and indexes.</returns>
        public string BuildCreate(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            Console.WriteLine($"[TRACE] In BuildCreate for: {entity.Name}");
            Console.WriteLine($"  Relationships: {entity.Relationships.Count}");
            foreach (var rel in entity.Relationships)
                Console.WriteLine($"    🔗 {rel.SourceEntity} {rel.Type} -> {rel.TargetEntity}");

            Console.WriteLine($"  CheckConstraints: {entity.CheckConstraints.Count}");
            foreach (var ck in entity.CheckConstraints)
                Console.WriteLine($"    ✅ {ck.Name}: {ck.Expression}");

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            var sb = new StringBuilder();

            var oldEntity = schemaReader.GetEntityDefinition(schema, entity.Name);

            // 🔧 ALTER COLUMN
            if (oldEntity != null)
            {
                foreach (var newCol in entity.Columns)
                {
                    var oldCol = oldEntity.Columns.FirstOrDefault(c => c.Name.Equals(newCol.Name, StringComparison.OrdinalIgnoreCase));
                    if (oldCol == null) continue;

                    var typeChanged = !string.Equals(oldCol.TypeName, newCol.TypeName, StringComparison.OrdinalIgnoreCase);
                    var nullabilityChanged = oldCol.IsNullable != newCol.IsNullable;

                    if (typeChanged || nullabilityChanged)
                    {
                        var nullability = newCol.IsNullable ? "NULL" : "NOT NULL";
                        sb.AppendLine($@"
-- 🔧 ALTER COLUMN: {newCol.Name}
ALTER TABLE [{schema}].[{entity.Name}]
ALTER COLUMN [{newCol.Name}] {newCol.TypeName} {nullability};");
                    }
                }
            }

            // 🔹 CHECK constraints
            foreach (var c in entity.CheckConstraints)
            {
                sb.AppendLine($@"
IF EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.name = N'{c.Name}'
      AND cc.parent_object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
BEGIN
    ALTER TABLE [{schema}].[{entity.Name}]
    DROP CONSTRAINT [{c.Name}];
END;

ALTER TABLE [{schema}].[{entity.Name}]
ADD CONSTRAINT [{c.Name}] CHECK ({c.Expression});");

                if (!string.IsNullOrWhiteSpace(c.Description))
                {
                    sb.AppendLine($@"
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'{c.Description}',
    @level0type = N'SCHEMA',    @level0name = N'{schema}',
    @level1type = N'TABLE',     @level1name = N'{entity.Name}',
    @level2type = N'CONSTRAINT',@level2name = N'{c.Name}';");
                }
            }

            // 🔹 Relationships
            foreach (var rel in entity.Relationships.Where(r => r.Type == RelationshipType.OneToOne))
            {
                var colName = entity.Columns
                    .FirstOrDefault(c =>
                        c.Name.Equals($"{rel.TargetEntity}Id", StringComparison.OrdinalIgnoreCase) ||
                        c.Name.Equals($"{rel.SourceEntity}Id", StringComparison.OrdinalIgnoreCase))?.Name;

                if (string.IsNullOrWhiteSpace(colName)) continue;

                var fkName = $"FK_{entity.Name}_{colName}";
                var uqName = $"UQ_{entity.Name}_{colName}";
                var ixName = $"IX_{entity.Name}_{colName}";
                var cascadeClause = rel.OnDelete == ReferentialAction.Cascade ? " ON DELETE CASCADE" : "";

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
FOREIGN KEY ([{colName}])
REFERENCES [{schema}].[{rel.TargetEntity}]([Id]){cascadeClause};

IF EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'{uqName}'
      AND object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
BEGIN
    ALTER TABLE [{schema}].[{entity.Name}]
    DROP CONSTRAINT [{uqName}];
END;

ALTER TABLE [{schema}].[{entity.Name}]
ADD CONSTRAINT [{uqName}]
UNIQUE ([{colName}]);

IF EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = N'{ixName}'
      AND object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
BEGIN
    DROP INDEX [{ixName}] ON [{schema}].[{entity.Name}];
END;

CREATE INDEX [{ixName}]
ON [{schema}].[{entity.Name}]([{colName}]);");
            }

            // 🔹 Explicit ForeignKeys
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
            }

            return sb.ToString().Trim();
        }
        //        /// <summary>
        //        /// Builds SQL ALTER TABLE statements to create all constraints for an entity.
        //        /// Includes CHECK, FOREIGN KEY, and UNIQUE constraints, with optional extended descriptions.
        //        /// </summary>
        //        public string BuildCreate(EntityDefinition entity)
        //        {
        //            if (entity == null) throw new ArgumentNullException(nameof(entity));

        //            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
        //            var sb = new StringBuilder();

        //            // 🔹 CHECK constraints
        //            if (entity.CheckConstraints != null)
        //            {
        //                foreach (var c in entity.CheckConstraints)
        //                {
        //                    sb.AppendLine($@"
        //IF NOT EXISTS (
        //    SELECT 1
        //    FROM sys.check_constraints cc
        //    WHERE cc.name = N'{EscapeSqlLiteral(c.Name)}'
        //      AND cc.parent_object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
        //)
        //BEGIN
        //    ALTER TABLE [{schema}].[{entity.Name}] 
        //        ADD CONSTRAINT [{c.Name}] CHECK ({c.Expression});
        //END;");

        //                    if (!string.IsNullOrWhiteSpace(c.Description))
        //                    {
        //                        sb.AppendLine($@"
        //EXEC sys.sp_addextendedproperty 
        //    @name = N'MS_Description',
        //    @value = N'{EscapeSqlLiteral(c.Description)}',
        //    @level0type = N'SCHEMA',    @level0name = N'{EscapeSqlLiteral(schema)}',
        //    @level1type = N'TABLE',     @level1name = N'{EscapeSqlLiteral(entity.Name)}',
        //    @level2type = N'CONSTRAINT',@level2name = N'{EscapeSqlLiteral(c.Name)}';");
        //                    }

        //                    sb.AppendLine();
        //                }
        //            }

        //            // 🔹 UNIQUE constraints from One-to-One relationships
        //            if (entity.Relationships != null)
        //            {
        //                foreach (var rel in entity.Relationships.Where(r => r.Type == RelationshipType.OneToOne))
        //                {
        //                    var fkColumn = $"{rel.SourceEntity}Id";

        //                    sb.AppendLine($@"
        //ALTER TABLE [{schema}].[{rel.TargetEntity}]
        //ADD CONSTRAINT [FK_{rel.TargetEntity}_{fkColumn}]
        //FOREIGN KEY ([{fkColumn}])
        //REFERENCES [{schema}].[{rel.SourceEntity}]([Id]);");

        //                    sb.AppendLine($@"
        //ALTER TABLE [{schema}].[{rel.TargetEntity}]
        //ADD CONSTRAINT [UQ_{rel.TargetEntity}_{fkColumn}]
        //UNIQUE ([{fkColumn}]);");
        //                }
        //            }

        //            // 🔹 FOREIGN KEY constraints from ForeignKeys
        //            if (entity.ForeignKeys != null)
        //            {
        //                foreach (var fk in entity.ForeignKeys)
        //                {
        //                    var referencedColumn = fk.ReferencedColumn ?? "Id";

        //                    sb.AppendLine($@"
        //ALTER TABLE [{schema}].[{entity.Name}]
        //ADD CONSTRAINT [{fk.ConstraintName}]
        //FOREIGN KEY ([{fk.Column}])
        //REFERENCES [{schema}].[{fk.ReferencedTable}]([{referencedColumn}]);");
        //                }
        //            }

        //            return sb.ToString().Trim();
        //        }


        /// <summary>
        /// Generates DROP CHECK constraint scripts from an <see cref="EntityDefinition"/>.
        /// Adds IF EXISTS safety checks.
        /// </summary>
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
        /// Escapes single quotes for safe inclusion in SQL string literals.
        /// </summary>
        private static string EscapeSqlLiteral(string input) =>
            (input ?? string.Empty).Replace("'", "''");
    }
}