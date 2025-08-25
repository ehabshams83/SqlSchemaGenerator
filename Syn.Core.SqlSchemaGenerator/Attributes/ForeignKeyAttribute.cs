namespace Syn.Core.SqlSchemaGenerator.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyAttribute : Attribute
{
    public string TargetTable { get; }
    public string TargetColumn { get; }

    public ForeignKeyAttribute(string name, string targetColumn = "Id")
    {
        TargetTable = name;
        TargetColumn = targetColumn;
    }
}
