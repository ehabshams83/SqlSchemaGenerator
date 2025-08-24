using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;

using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Storage
{
    /// <summary>
    /// Stores schema snapshots in a SQL Server table using JSON serialization.
    /// </summary>
    public class SqlSchemaSnapshotStore : ISchemaSnapshotStore
    {
        private readonly string _connectionString;
        private const string TableName = "__SchemaSnapshots";

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlSchemaSnapshotStore"/> class.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        public SqlSchemaSnapshotStore(string connectionString)
        {
            _connectionString = connectionString;
            EnsureSnapshotTableExists();
        }

        /// <inheritdoc />
        public void SaveSnapshot(string version, IReadOnlyList<EntityDefinition> entities)
        {
            var json = JsonSerializer.Serialize(entities);
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand($@"
                INSERT INTO {TableName} (Version, SnapshotJson, CreatedAt)
                VALUES (@Version, @SnapshotJson, GETUTCDATE())", connection);

            command.Parameters.AddWithValue("@Version", version);
            command.Parameters.AddWithValue("@SnapshotJson", json);

            connection.Open();
            command.ExecuteNonQuery();
        }

        /// <inheritdoc />
        public IReadOnlyList<EntityDefinition>? GetSnapshot(string version)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand($@"
                SELECT SnapshotJson FROM {TableName}
                WHERE Version = @Version", connection);

            command.Parameters.AddWithValue("@Version", version);
            connection.Open();

            var result = command.ExecuteScalar() as string;
            return result is null
                ? null
                : JsonSerializer.Deserialize<List<EntityDefinition>>(result);
        }

        /// <inheritdoc />
        public IReadOnlyList<EntityDefinition>? GetLatestSnapshot()
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand($@"
                SELECT TOP 1 SnapshotJson FROM {TableName}
                ORDER BY CreatedAt DESC", connection);

            connection.Open();
            var result = command.ExecuteScalar() as string;
            return result is null
                ? null
                : JsonSerializer.Deserialize<List<EntityDefinition>>(result);
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ListVersions()
        {
            var versions = new List<string>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand($@"
                SELECT Version FROM {TableName}
                ORDER BY CreatedAt ASC", connection);

            connection.Open();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                versions.Add(reader.GetString(0));
            }

            return versions;
        }

        /// <summary>
        /// Ensures the snapshot table exists in the database.
        /// </summary>
        private void EnsureSnapshotTableExists()
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand($@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{TableName}')
                BEGIN
                    CREATE TABLE {TableName} (
                        Version NVARCHAR(100) PRIMARY KEY,
                        SnapshotJson NVARCHAR(MAX),
                        CreatedAt DATETIME
                    )
                END", connection);

            connection.Open();
            command.ExecuteNonQuery();
        }
    }
}