namespace Syn.Core.SqlSchemaGenerator.Models;

public class RelationshipDefinition
{
    public string SourceEntity { get; set; }           // الكيان اللي يحتوي العلاقة
    public string TargetEntity { get; set; }           // الكيان المرتبط
    public string SourceProperty { get; set; }         // اسم الـ Property في المصدر
    public RelationshipType Type { get; set; }         // نوع العلاقة
    public string? JoinEntityName { get; set; }        // اسم الجدول الوسيط لو Many-to-Many
    public bool IsExplicitJoinEntity { get; set; }     // هل الجدول الوسيط معرف بكلاس
}

public enum RelationshipType
{
    OneToOne,
    OneToMany,
    ManyToOne,
    ManyToMany
}
