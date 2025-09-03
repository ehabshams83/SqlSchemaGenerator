using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using System.Diagnostics;

namespace Syn.Core.SqlSchemaGenerator.Execution
{
    /// <summary>
    /// Executes SQL scripts with support for batching via GO, transaction safety, and async execution.
    /// </summary>
    public class SqlScriptRunner
    {
        private readonly ILogger<SqlScriptRunner> _logger;

        /// <summary>
        /// Timeout in seconds for each SQL batch. Default is 30 seconds.
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        public SqlScriptRunner(ILogger<SqlScriptRunner> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Executes a SQL script on the target database, splitting by GO and wrapping in a transaction.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="script">The SQL script to execute.</param>
        /// <param name="externalTransaction">Optional existing transaction to use.</param>
        /// <returns>Execution result containing stats and errors.</returns>
        public async Task<SqlScriptExecutionResult> ExecuteScriptAsync(
            string connectionString,
            string script,
            SqlTransaction externalTransaction = null)
        {
            var result = new SqlScriptExecutionResult();
            var stopwatch = Stopwatch.StartNew();

            var batches = SplitScriptByGo(script);
            result.TotalBatches = batches.Count;

            SqlConnection connection = null;
            SqlTransaction transaction = externalTransaction;

            try
            {
                if (externalTransaction == null)
                {
                    connection = new SqlConnection(connectionString);
                    await connection.OpenAsync();
                    transaction = connection.BeginTransaction();
                }
                else
                {
                    connection = externalTransaction.Connection!;
                }

                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;

                    try
                    {
                        using var command = new SqlCommand(batch, connection, transaction)
                        {
                            CommandTimeout = CommandTimeout
                        };
                        await command.ExecuteNonQueryAsync();
                        result.ExecutedBatches++;
                        _logger?.LogDebug("Executed batch:\n{Batch}", batch);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(ex.Message);
                        _logger?.LogError(ex, "Error executing batch.");
                        throw;
                    }
                }

                if (externalTransaction == null)
                    transaction?.Commit();

                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                _logger?.LogInformation("All batches executed successfully in {Duration} ms.", result.DurationMs);
            }
            catch
            {
                if (externalTransaction == null)
                    transaction?.Rollback();

                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                _logger?.LogWarning("Execution failed after {Duration} ms. Transaction rolled back.", result.DurationMs);
                throw;
            }
            finally
            {
                if (externalTransaction == null)
                {
                    transaction?.Dispose();
                    connection?.Dispose();
                }
            }

            return result;
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

    /// <summary>
    /// Represents the result of executing a SQL script.
    /// </summary>
    public class SqlScriptExecutionResult
    {
        public int TotalBatches { get; set; }
        public int ExecutedBatches { get; set; }
        public long DurationMs { get; set; }
        public List<string> Errors { get; } = new();
        public bool Success => Errors.Count == 0;
    }
}