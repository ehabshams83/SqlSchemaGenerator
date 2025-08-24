using System.Reflection;

using Syn.Core.SqlSchemaGenerator.Builders;

namespace Syn.Core.SqlSchemaGenerator.Execution
{
    /// <summary>
    /// Executes schema generation by scanning assemblies and building SQL scripts.
    /// </summary>
    public class SqlSchemaExecutor
    {
        private readonly SqlTableBuilder _tableBuilder = new();
        private readonly SqlDropTableBuilder _dropBuilder = new();

        /// <summary>
        /// Generates CREATE TABLE scripts for all public classes in the given assembly.
        /// </summary>
        public string GenerateCreateScripts(Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic)
                .ToList();

            var scripts = types
                .Select(t => _tableBuilder.Build(t))
                .ToList();

            return string.Join("\n\n", scripts);
        }

        /// <summary>
        /// Generates DROP TABLE scripts for all public classes in the given assembly.
        /// </summary>
        public string GenerateDropScripts(Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic)
                .ToList();

            var scripts = types
                .Select(t => _dropBuilder.Build(t.Name))
                .ToList();

            return string.Join("\n", scripts);
        }

        /// <summary>
        /// Generates CREATE TABLE script for a single entity type.
        /// </summary>
        public string GenerateCreateScript(Type entityType) =>
            _tableBuilder.Build(entityType);

        /// <summary>
        /// Generates DROP TABLE script for a single entity type.
        /// </summary>
        public string GenerateDropScript(Type entityType) =>
            _dropBuilder.Build(entityType.Name);
    }
}