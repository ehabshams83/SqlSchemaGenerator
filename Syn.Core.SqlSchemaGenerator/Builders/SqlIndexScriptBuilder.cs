using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Linq;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Builds SQL scripts for creating and dropping indexes from an entity model.
    /// Supports building from either a CLR <see cref="Type"/> or an <see cref="EntityDefinition"/>.
    /// </summary>
    public class SqlIndexScriptBuilder
    {
        private readonly EntityDefinitionBuilder _entityDefinitionBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlIndexScriptBuilder"/> class.
        /// </summary>
        /// <param name="entityDefinitionBuilder">The unified entity definition builder.</param>
        public SqlIndexScriptBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
        {
            _entityDefinitionBuilder = entityDefinitionBuilder
                ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        }

        /// <summary>
        /// Generates CREATE INDEX scripts from a CLR type.
        /// </summary>
        public string BuildCreate(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            return BuildCreate(_entityDefinitionBuilder.BuildAllWithRelationships(new[] { entityType }).First());
        }

        /// <summary>
        /// Generates DROP INDEX scripts from a CLR type.
        /// </summary>
        public string BuildDrop(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            return BuildDrop(_entityDefinitionBuilder.BuildAllWithRelationships(new[] { entityType }).First());
        }

        /// <summary>
        /// Generates CREATE INDEX scripts from an existing <see cref="EntityDefinition"/>.
        /// </summary>
        public string BuildCreate(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Indexes == null || !entity.Indexes.Any()) return string.Empty;

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;

            return string.Join(Environment.NewLine, entity.Indexes.Select(index =>
                $"CREATE {(index.IsUnique ? "UNIQUE " : "")}INDEX [{index.Name}] " +
                $"ON [{schema}].[{entity.Name}] " +
                $"({string.Join(", ", index.Columns.Select(c => $"[{c}]"))});"
            ));
        }

        /// <summary>
        /// Generates DROP INDEX scripts from an existing <see cref="EntityDefinition"/>.
        /// </summary>
        public string BuildDrop(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Indexes == null || !entity.Indexes.Any()) return string.Empty;

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;

            return string.Join(Environment.NewLine, entity.Indexes.Select(index =>
                $"DROP INDEX IF EXISTS [{index.Name}] ON [{schema}].[{entity.Name}];"
            ));
        }
    }
}