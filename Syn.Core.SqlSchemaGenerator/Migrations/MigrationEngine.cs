using Syn.Core.SqlSchemaGenerator.Migrations.Steps;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Scanning;
using Syn.Core.SqlSchemaGenerator.Sql;

using System.Reflection;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Migrations;

/// <summary>
/// Core engine responsible for scanning entities and generating migration steps.
/// </summary>
public class MigrationEngine
{
    private readonly EntityScanner _scanner;
    private readonly ISchemaSnapshotProvider _snapshotProvider;
    private readonly IMigrationStepBuilder _stepBuilder;
    private readonly SqlMigrationGenerator _sqlGenerator = new();

    public MigrationEngine(
        EntityScanner scanner,
        ISchemaSnapshotProvider snapshotProvider,
        IMigrationStepBuilder stepBuilder)
    {
        _scanner = scanner;
        _snapshotProvider = snapshotProvider;
        _stepBuilder = stepBuilder;
    }

    /// <summary>
    /// Scans the given assemblies and generates migration steps based on schema differences.
    /// </summary>
    public List<MigrationStep> GenerateMigrations(params Assembly[] assemblies)
    {
        var currentEntities = _scanner.Scan(assemblies);
        var previousSnapshot = _snapshotProvider.LoadSnapshot();

        var steps = _stepBuilder.BuildSteps(previousSnapshot, currentEntities);

        return steps;
    }

    /// <summary>
    /// Generates a full SQL script from schema differences in the given assemblies.
    /// </summary>
    public string GenerateScript(params Assembly[] assemblies)
    {
        var steps = GenerateMigrations(assemblies);
        var sb = new StringBuilder();
        sb.AppendLine("-- Begin Migration Script");

        foreach (var step in steps)
        {
            var sql = _sqlGenerator.GenerateSql(step);
            sb.AppendLine(sql);
            sb.AppendLine();
        }

        sb.AppendLine("-- End Migration Script");
        return sb.ToString();
    }
}