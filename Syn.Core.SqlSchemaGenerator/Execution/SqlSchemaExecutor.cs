using System;
using System.Linq;
using System.Reflection;

using Syn.Core.SqlSchemaGenerator.Builders;
using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Execution
{
    /// <summary>
    /// Executes schema generation and migration by scanning assemblies
    /// and delegating SQL generation to the specialized builders.
    /// </summary>
    public class SqlSchemaExecutor
    {
        private readonly EntityDefinitionBuilder _entityDefinitionBuilder;

        private readonly SqlTableScriptBuilder _tableBuilder;
        private readonly SqlDropTableBuilder _dropTableBuilder;
        private readonly SqlIndexScriptBuilder _indexBuilder;
        private readonly SqlConstraintScriptBuilder _constraintBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlSchemaExecutor"/> class
        /// with all builders wired to a shared <see cref="EntityDefinitionBuilder"/>.
        /// </summary>
        public SqlSchemaExecutor(DatabaseSchemaReader schemaReader)
        {
            _entityDefinitionBuilder = new EntityDefinitionBuilder();
            _tableBuilder = new SqlTableScriptBuilder(_entityDefinitionBuilder);
            _dropTableBuilder = new SqlDropTableBuilder(_entityDefinitionBuilder);
            _indexBuilder = new SqlIndexScriptBuilder(_entityDefinitionBuilder);
            _constraintBuilder = new SqlConstraintScriptBuilder(_entityDefinitionBuilder, schemaReader);
        }

        /// <summary>
        /// Generates CREATE scripts (tables + indexes + constraints) for all public classes in the given assembly.
        /// </summary>
        public string GenerateCreateScripts(Assembly assembly)
        {
            var types = GetEntityTypes(assembly);

            var scripts = types.Select(t =>
                _tableBuilder.Build(t) + Environment.NewLine +
                _indexBuilder.BuildCreate(t) + Environment.NewLine +
                _constraintBuilder.BuildCreate(t)
            );

            return string.Join("\n\n", scripts.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        /// <summary>
        /// Generates DROP scripts (constraints + indexes + tables) for all public classes in the given assembly.
        /// </summary>
        public string GenerateDropScripts(Assembly assembly)
        {
            var types = GetEntityTypes(assembly);

            var scripts = types.Select(t =>
                _constraintBuilder.BuildDrop(t) + Environment.NewLine +
                _indexBuilder.BuildDrop(t) + Environment.NewLine +
                _dropTableBuilder.Build(t)
            );

            return string.Join("\n\n", scripts.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        /// <summary>
        /// Generates full CREATE script for a single entity type (table + indexes + constraints).
        /// </summary>
        public string GenerateCreateScript(Type entityType)
        {
            return string.Join("\n",
                _tableBuilder.Build(entityType),
                _indexBuilder.BuildCreate(entityType),
                _constraintBuilder.BuildCreate(entityType)
            ).Trim();
        }

        /// <summary>
        /// Generates full DROP script for a single entity type (constraints + indexes + table).
        /// </summary>
        public string GenerateDropScript(Type entityType)
        {
            return string.Join("\n",
                _constraintBuilder.BuildDrop(entityType),
                _indexBuilder.BuildDrop(entityType),
                _dropTableBuilder.Build(entityType)
            ).Trim();
        }

        /// <summary>
        /// A unified migration method that drops then recreates all schema objects for the given assembly.
        /// </summary>
        public string Migrate(Assembly assembly)
        {
            var drop = GenerateDropScripts(assembly);
            var create = GenerateCreateScripts(assembly);

            return string.Join("\nGO\n\n", new[] { drop, create }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        /// <summary>
        /// Retrieves all public, non-abstract classes from the assembly to treat as entities.
        /// </summary>
        private static Type[] GetEntityTypes(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
                .ToArray();
        }
    }
}