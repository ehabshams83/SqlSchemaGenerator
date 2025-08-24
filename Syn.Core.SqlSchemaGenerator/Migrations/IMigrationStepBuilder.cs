using Syn.Core.SqlSchemaGenerator.Migrations.Steps;
using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Migrations
{
    public interface IMigrationStepBuilder
    {
        List<MigrationStep> BuildSteps(List<EntityDefinition> oldSchema, List<EntityDefinition> newSchema);
    }

}
