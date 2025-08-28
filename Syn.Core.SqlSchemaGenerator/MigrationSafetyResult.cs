namespace Syn.Core.SqlSchemaGenerator;

/// <summary>
/// Represents the result of analyzing a migration script for safety.
/// </summary>
public class MigrationSafetyResult
{
    public bool IsSafe { get; set; }
    public List<string> SafeCommands { get; set; } = new();
    public List<string> UnsafeCommands { get; set; } = new();
    public List<string> Reasons { get; set; } = new();
}
