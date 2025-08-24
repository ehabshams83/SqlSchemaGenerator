
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Syn.Core.SqlSchemaGenerator.Deployment
{
    /// <summary>
    /// Executes SQL scripts with support for batching via GO and transaction safety.
    /// </summary>
    public class SqlScriptRunner
    {
        private readonly ILogger<SqlScriptRunner> _logger;
        /// <summary>
        /// Timeout in seconds for each SQL batch. Default is 30 seconds.
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        public SqlScriptRunner(ILogger<SqlScriptRunner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Executes a SQL script on the target database, splitting by GO and wrapping in a transaction.
        /// </summary>
        public void ExecuteScript(string connectionString, string script)
        {
            var batches = SplitScriptByGo(script);

            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;

                    using var command = new SqlCommand(batch, connection, transaction)
                    {
                        CommandTimeout = CommandTimeout
                    };

                    command.ExecuteNonQuery();

                    _logger.LogDebug("Executed batch:\n{Batch}", batch);
                }

                transaction.Commit();
                _logger.LogInformation("All batches executed successfully and transaction committed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL script. Rolling back transaction.");
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Splits a SQL script into batches using GO as a delimiter.
        /// </summary>
        private List<string> SplitScriptByGo(string script)
        {
            var lines = script.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var batches = new List<string>();
            var currentBatch = new List<string>();

            foreach (var line in lines)
            {
                if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    batches.Add(string.Join("\n", currentBatch));
                    currentBatch.Clear();
                }
                else
                {
                    currentBatch.Add(line);
                }
            }

            if (currentBatch.Count > 0)
                batches.Add(string.Join("\n", currentBatch));

            return batches;
        }
    }
}