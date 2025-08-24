using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Migrations;

namespace Syn.Core.SqlSchemaGenerator.Snapshots
{
    /// <summary>
    /// Provides a static in-memory snapshot of entity definitions for migration comparison.
    /// </summary>
    public class InMemorySnapshotProvider : ISchemaSnapshotProvider
    {
        private readonly List<EntityDefinition> _snapshot;

        /// <summary>
        /// Initializes the provider with a predefined snapshot.
        /// </summary>
        /// <param name="entities">The entity definitions representing the current schema.</param>
        public InMemorySnapshotProvider(IEnumerable<EntityDefinition> entities)
        {
            _snapshot = entities.ToList();
        }

        /// <summary>
        /// Loads the snapshot from memory.
        /// </summary>
        /// <returns>List of entity definitions.</returns>
        public List<EntityDefinition> LoadSnapshot()
        {
            return _snapshot;
        }
    }
}