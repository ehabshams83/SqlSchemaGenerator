namespace Syn.Core.SqlSchemaGenerator.Storage;

/// <summary>
/// Determines how migration versions are generated.
/// </summary>
public enum MigrationVersionMode
{
    /// <summary>
    /// Single version for the entire migration run.
    /// </summary>
    SingleBatch,

    /// <summary>
    /// Separate version for each entity.
    /// </summary>
    PerEntity,

    /// <summary>
    /// Separate version for each logical batch (e.g., tables, views, indexes).
    /// </summary>
    PerLogicalBatch
}
