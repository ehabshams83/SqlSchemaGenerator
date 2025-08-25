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

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlConstraintScriptBuilder"/> class.
        /// </summary>
        /// <param name="entityDefinitionBuilder">The unified entity definition builder.</param>
        public SqlConstraintScriptBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
        {
            _entityDefinitionBuilder = entityDefinitionBuilder
                ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
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
        /// Generates CREATE CHECK constraint scripts from an <see cref="EntityDefinition"/>.
        /// Adds IF NOT EXISTS safety checks and optional extended property for descriptions.
        /// </summary>
        public string BuildCreate(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.CheckConstraints == null || !entity.CheckConstraints.Any()) return string.Empty;

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            var sb = new StringBuilder();

            foreach (var c in entity.CheckConstraints)
            {
                sb.AppendLine($@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints cc
    WHERE cc.name = N'{EscapeSqlLiteral(c.Name)}'
      AND cc.parent_object_id = OBJECT_ID(N'[{schema}].[{entity.Name}]')
)
BEGIN
    ALTER TABLE [{schema}].[{entity.Name}] 
        ADD CONSTRAINT [{c.Name}] CHECK ({c.Expression});
END;
".Trim());

                if (!string.IsNullOrWhiteSpace(c.Description))
                {
                    sb.AppendLine($@"
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'{EscapeSqlLiteral(c.Description)}',
    @level0type = N'SCHEMA',    @level0name = N'{EscapeSqlLiteral(schema)}',
    @level1type = N'TABLE',     @level1name = N'{EscapeSqlLiteral(entity.Name)}',
    @level2type = N'CONSTRAINT',@level2name = N'{EscapeSqlLiteral(c.Name)}';
".Trim());
                }

                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

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