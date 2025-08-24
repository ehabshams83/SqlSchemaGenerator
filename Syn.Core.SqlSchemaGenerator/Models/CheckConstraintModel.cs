namespace Syn.Core.SqlSchemaGenerator.Models
{
    public class CheckConstraintModel
    {
        public string Expression { get; set; } = null!;
        public string? Name { get; set; }
    }
}
