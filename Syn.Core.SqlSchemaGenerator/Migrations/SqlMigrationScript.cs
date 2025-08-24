namespace Syn.Core.SqlSchemaGenerator.Migrations;

/// <summary>
/// Represents a SQL migration script tied to a specific entity and version.
/// </summary>
public class SqlMigrationScript
{
    public string EntityName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// Computes a SHA256 hash of the SQL script to uniquely identify it.
    /// </summary>
    public string Hash => ComputeHash(Sql);

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}