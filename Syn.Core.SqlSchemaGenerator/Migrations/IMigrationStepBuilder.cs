using Syn.Core.SqlSchemaGenerator.Migrations.Steps;
using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Migrations
{
    public interface IMigrationStepBuilder
    {
        List<MigrationStep> BuildSteps(IEnumerable<EntityDefinition> oldSchema, IEnumerable<EntityDefinition> newSchema);
    }

}
