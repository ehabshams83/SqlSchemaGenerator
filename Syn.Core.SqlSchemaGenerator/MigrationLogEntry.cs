namespace Syn.Core.SqlSchemaGenerator;

/// <summary>
/// Represents a single executed migration command with timestamp and status.
/// </summary>
public class MigrationLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Summary { get; set; }
    public string FullCommand { get; set; }
    public string Status { get; set; } // e.g. Executed, Skipped, Failed
}