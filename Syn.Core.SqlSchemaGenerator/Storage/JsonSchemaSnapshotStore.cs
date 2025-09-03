using Syn.Core.SqlSchemaGenerator.Models;

using System.Security.Cryptography;

namespace Syn.Core.SqlSchemaGenerator.Storage;

/// <summary>
/// Default JSON-based implementation of ISchemaSnapshotStore.
/// Stores snapshots as JSON files in a specified folder.
/// </summary>
public class JsonSchemaSnapshotStore : ISchemaSnapshotStore
{
    private readonly string _snapshotFolder;

    public JsonSchemaSnapshotStore(string snapshotFolder)
    {
        _snapshotFolder = snapshotFolder;
    }

    public void SaveSnapshot(string version, IReadOnlyList<EntityDefinition> entities)
    {
        Directory.CreateDirectory(_snapshotFolder);
        var path = Path.Combine(_snapshotFolder, $"snapshot_{version}.json");

        var ordered = entities
            .OrderBy(e => e.Schema).ThenBy(e => e.Name)
            .Select(e => new
            {
                e.Schema,
                e.Name,
                e.Description,
                Columns = e.Columns.OrderBy(c => c.Name),
                Constraints = e.Constraints.OrderBy(c => c.Name),
                CheckConstraints = e.CheckConstraints.OrderBy(c => c.Name),
                Indexes = e.Indexes.OrderBy(i => i.Name)
            });

        var json = System.Text.Json.JsonSerializer.Serialize(
            ordered, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(path, json);
    }

    public IReadOnlyList<EntityDefinition>? GetSnapshot(string version)
    {
        var path = Path.Combine(_snapshotFolder, $"snapshot_{version}.json");
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<List<EntityDefinition>>(json);
    }

    public IReadOnlyList<EntityDefinition>? GetLatestSnapshot()
    {
        if (!Directory.Exists(_snapshotFolder))
            return null;

        var latestFile = Directory.GetFiles(_snapshotFolder, "snapshot_*.json")
            .OrderByDescending(f => f)
            .FirstOrDefault();

        if (latestFile == null)
            return null;

        var json = File.ReadAllText(latestFile);
        return System.Text.Json.JsonSerializer.Deserialize<List<EntityDefinition>>(json);
    }

    public IReadOnlyList<string> ListVersions()
    {
        if (!Directory.Exists(_snapshotFolder))
            return Array.Empty<string>();

        return Directory.GetFiles(_snapshotFolder, "snapshot_*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f).Replace("snapshot_", ""))
            .OrderBy(v => v)
            .ToList();
    }

    public byte[]? ComputeSnapshotHash(string version)
    {
        var path = Path.Combine(_snapshotFolder, $"snapshot_{version}.json");
        if (!File.Exists(path))
            return null;

        using var sha = SHA256.Create();
        return sha.ComputeHash(File.ReadAllBytes(path));
    }

    public bool DeleteSnapshot(string version)
    {
        var path = Path.Combine(_snapshotFolder, $"snapshot_{version}.json");
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }
}
