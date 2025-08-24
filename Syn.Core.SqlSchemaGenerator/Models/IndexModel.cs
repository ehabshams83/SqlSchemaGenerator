namespace Syn.Core.SqlSchemaGenerator.Models
{
    public class IndexModel
    {
        public string? Name { get; set; }
        public bool IsUnique { get; set; }
        public List<string> Columns { get; set; } = new();
    }

}
