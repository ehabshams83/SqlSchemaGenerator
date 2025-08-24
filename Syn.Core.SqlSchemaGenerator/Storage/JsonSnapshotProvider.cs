using Syn.Core.SqlSchemaGenerator.Migrations;
using Syn.Core.SqlSchemaGenerator.Models;
using System.Text.Json;

namespace Syn.Core.SqlSchemaGenerator.Storage
{
    public class JsonSnapshotProvider : ISchemaSnapshotProvider
    {
        private readonly string _filePath;

        public JsonSnapshotProvider(string filePath)
        {
            _filePath = filePath;
        }

        public IEnumerable<EntityDefinition> LoadSnapshot()
        {
            if (!File.Exists(_filePath))
                return new List<EntityDefinition>();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<EntityDefinition>>(json)
                   ?? new List<EntityDefinition>();
        }

        public void SaveSnapshot(IEnumerable<EntityDefinition> entities)
        {
            var json = JsonSerializer.Serialize(entities, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_filePath, json);
        }
    }
}