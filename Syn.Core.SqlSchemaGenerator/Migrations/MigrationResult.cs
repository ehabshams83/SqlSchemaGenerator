namespace Syn.Core.SqlSchemaGenerator.Migrations;

/// <summary>
/// Represents the result of applying a set of migration scripts.
/// </summary>
public class MigrationResult
{
    public List<string> ExecutedScripts { get; set; } = new();
    public List<string> SkippedScripts { get; set; } = new();
}