using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Migrations
{
    public interface ISchemaSnapshotProvider
    {
        List<EntityDefinition> LoadSnapshot();
    }

}
