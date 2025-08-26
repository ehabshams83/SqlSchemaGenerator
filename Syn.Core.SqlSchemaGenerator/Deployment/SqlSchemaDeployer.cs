using System.Reflection;

using Microsoft.Extensions.Logging;

using Syn.Core.SqlSchemaGenerator.Execution;

namespace Syn.Core.SqlSchemaGenerator.Deployment
{
    /// <summary>
    /// Deploys SQL schema scripts to a target database with logging and error handling.
    /// </summary>
    public class SqlSchemaDeployer
    {
        private readonly SqlSchemaExecutor _executor;
        private readonly SqlScriptRunner _runner;
        private readonly ILogger<SqlSchemaDeployer> _logger;

        /// <summary>
        /// Initializes a new instance of the SqlSchemaDeployer.
        /// </summary>
        public SqlSchemaDeployer(ILogger<SqlSchemaDeployer> logger, DatabaseSchemaReader schemaReader)
        {
            _executor = new SqlSchemaExecutor(schemaReader);
            var loggerFactory = LoggerFactory.Create(builder => { });
            var __logger = loggerFactory.CreateLogger<SqlScriptRunner>();
            _runner = new SqlScriptRunner(__logger);
            _logger = logger;
        }

        /// <summary>
        /// Deploys CREATE TABLE scripts for all entities in the given assembly.
        /// </summary>
        public void DeployCreateScripts(Assembly assembly, string connectionString)
        {
            try
            {
                _logger.LogInformation("Starting schema deployment for assembly: {Assembly}", assembly.FullName);

                var script = _executor.GenerateCreateScripts(assembly);

                if (string.IsNullOrWhiteSpace(script))
                {
                    _logger.LogWarning("No CREATE scripts generated. Deployment aborted.");
                    return;
                }

                _logger.LogDebug("Generated CREATE script:\n{Script}", script);

                _runner.ExecuteScript(connectionString, script);

                _logger.LogInformation("Schema deployment completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during schema deployment.");
                throw;
            }
        }

        /// <summary>
        /// Deploys DROP TABLE scripts for all entities in the given assembly.
        /// </summary>
        public void DeployDropScripts(Assembly assembly, string connectionString)
        {
            try
            {
                _logger.LogInformation("Starting DROP script execution for assembly: {Assembly}", assembly.FullName);

                var script = _executor.GenerateDropScripts(assembly);

                if (string.IsNullOrWhiteSpace(script))
                {
                    _logger.LogWarning("No DROP scripts generated. Operation aborted.");
                    return;
                }

                _logger.LogDebug("Generated DROP script:\n{Script}", script);

                _runner.ExecuteScript(connectionString, script);

                _logger.LogInformation("DROP script executed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DROP script execution.");
                throw;
            }
        }
    }
}