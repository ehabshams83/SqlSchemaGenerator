using Microsoft.Extensions.Logging;

using Syn.Core.SqlSchemaGenerator.Execution;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Storage;

namespace Syn.Core.SqlSchemaGenerator.SchemaDeployment;

/// <summary>
/// Deploys SQL schema scripts to a target database with logging and optional migration tracking.
/// </summary>
public class SqlSchemaDeployer
{
    private readonly SqlSchemaExecutor _executor;
    private readonly SqlScriptRunner _runner;
    private readonly ILogger<SqlSchemaDeployer> _logger;
    private readonly string _connectionString;
    private readonly ISchemaSnapshotStore _snapshotStore;

    public SqlSchemaDeployer(
        ILogger<SqlSchemaDeployer> logger,
        DatabaseSchemaReader schemaReader,
        string connectionString,
        ISchemaSnapshotStore snapshotStore = null)
    {
        _executor = new SqlSchemaExecutor(schemaReader);
        _runner = new SqlScriptRunner();
        _logger = logger;
        _connectionString = connectionString;
        _snapshotStore = snapshotStore ?? new JsonSchemaSnapshotStore(@"C:\Snapshots");
    }

    // =======================
    // CREATE SCRIPTS - ASYNC
    // =======================
    public async Task DeployCreateScriptsAsync(IEnumerable<Type> types, bool trackMigration = false, string logicalGroup = null, MigrationSettings settings = null)
    {
        try
        {
            _logger.LogInformation("Starting schema deployment for types: {Types}", string.Join(", ", types.Select(t => t.Name)));

            var script = _executor.GenerateCreateScripts(types);
            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogWarning("No CREATE scripts generated. Deployment aborted.");
                return;
            }

            _logger.LogDebug("Generated CREATE script:\n{Script}", script);

            MigrationHistoryStore history = null;
            string version = null;

            if (trackMigration)
            {
                history = new MigrationHistoryStore(_connectionString, _snapshotStore, settings ?? new MigrationSettings());
                (var isNew, version) = history.EnsureTableAndInsertPending(script, null, logicalGroup);
                if (!isNew)
                {
                    _logger.LogWarning("Migration version already exists. Skipping execution.");
                    return;
                }
            }

            var result = await _runner.ExecuteScriptAsync(_connectionString, script);
            _logger.LogInformation("✅ Executed {Executed}/{Total} batches in {Duration} ms",
                result.ExecutedBatches, result.TotalBatches, result.DurationMs);

            if (trackMigration && history != null && version != null)
            {
                history.MarkApplied(version, (int)result.DurationMs, new List<EntityDefinition>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during schema deployment.");
            throw;
        }
    }

    // =======================
    // CREATE SCRIPTS - SYNC
    // =======================
    public void DeployCreateScripts(IEnumerable<Type> types, bool trackMigration = false, string logicalGroup = null, MigrationSettings settings = null)
    {
        DeployCreateScriptsAsync(types, trackMigration, logicalGroup, settings).GetAwaiter().GetResult();
    }

    // =======================
    // DROP SCRIPTS - ASYNC
    // =======================
    public async Task DeployDropScriptsAsync(IEnumerable<Type> types, bool trackMigration = false, string logicalGroup = null, MigrationSettings settings = null)
    {
        try
        {
            _logger.LogInformation("Starting DROP script execution for types: {Types}", string.Join(", ", types.Select(t => t.Name)));

            var script = _executor.GenerateDropScripts(types);
            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogWarning("No DROP scripts generated. Operation aborted.");
                return;
            }

            _logger.LogDebug("Generated DROP script:\n{Script}", script);

            MigrationHistoryStore history = null;
            string version = null;

            if (trackMigration)
            {
                history = new MigrationHistoryStore(_connectionString, _snapshotStore, settings ?? new MigrationSettings());
                (var isNew, version) = history.EnsureTableAndInsertPending(script, null, logicalGroup);
                if (!isNew)
                {
                    _logger.LogWarning("Migration version already exists. Skipping execution.");
                    return;
                }
            }

            var result = await _runner.ExecuteScriptAsync(_connectionString, script);
            _logger.LogInformation("✅ Executed {Executed}/{Total} batches in {Duration} ms",
                result.ExecutedBatches, result.TotalBatches, result.DurationMs);

            if (trackMigration && history != null && version != null)
            {
                history.MarkApplied(version, (int)result.DurationMs, new List<EntityDefinition>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during DROP script execution.");
            throw;
        }
    }

    // =======================
    // DROP SCRIPTS - SYNC
    // =======================
    public void DeployDropScripts(IEnumerable<Type> types, bool trackMigration = false, string logicalGroup = null, MigrationSettings settings = null)
    {
        DeployDropScriptsAsync(types, trackMigration, logicalGroup, settings).GetAwaiter().GetResult();
    }
}

