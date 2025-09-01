using Syn.Core.SqlSchemaGenerator.Models;

using System;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Builds SQL DROP TABLE scripts based on an EntityDefinition model.
    /// </summary>
    public class SqlDropTableBuilder
    {
        private readonly EntityDefinitionBuilder _entityDefinitionBuilder;

        /// <summary>
        /// Initializes a new instance of <see cref="SqlDropTableBuilder"/>.
        /// </summary>
        public SqlDropTableBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
        {
            _entityDefinitionBuilder = entityDefinitionBuilder
                ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        }

        /// <summary>
        /// Generates a DROP TABLE SQL script from a CLR type.
        /// </summary>
        public string Build(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            var entityDef = _entityDefinitionBuilder
                .BuildAllWithRelationships(new[] { entityType })
                .First();
            return Build(entityDef);
        }

        /// <summary>
        /// Generates a DROP TABLE SQL script from an EntityDefinition.
        /// </summary>
        public string Build(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            return $@"
IF OBJECT_ID(N'[{schema}].[{entity.Name}]', N'U') IS NOT NULL
    DROP TABLE [{schema}].[{entity.Name}];
".Trim();
        }
    }
}