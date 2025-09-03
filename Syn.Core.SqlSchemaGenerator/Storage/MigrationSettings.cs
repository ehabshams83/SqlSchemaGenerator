namespace Syn.Core.SqlSchemaGenerator.Storage;

/// <summary>
/// Global settings for migration execution.
/// </summary>
public class MigrationSettings
{
    /// <summary>
    /// Controls how migration versions are generated.
    /// Default is SingleBatch.
    /// </summary>
    public MigrationVersionMode VersionMode { get; set; } = MigrationVersionMode.SingleBatch;
}

