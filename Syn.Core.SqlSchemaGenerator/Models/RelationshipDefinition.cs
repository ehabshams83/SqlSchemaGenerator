namespace Syn.Core.SqlSchemaGenerator.Models;

public class RelationshipDefinition
{
    public string SourceEntity { get; set; }           // الكيان اللي يحتوي العلاقة
    public string TargetEntity { get; set; }           // الكيان المرتبط
    public string SourceProperty { get; set; }         // اسم الـ Property في المصدر

    /// <summary>
    /// اسم العمود في الكيان المصدر الذي يمثل المفتاح الأجنبي المرتبط بالكيان الهدف.
    /// مثال: UserId في Order.
    /// </summary>
    public string SourceToTargetColumn { get; set; }

    public RelationshipType Type { get; set; }         // نوع العلاقة
    public string? JoinEntityName { get; set; }        // اسم الجدول الوسيط لو Many-to-Many
    public bool IsExplicitJoinEntity { get; set; }     // هل الجدول الوسيط معرف بكلاس

    /// <summary>
    /// The action to perform on delete (e.g., CASCADE, SET NULL, NO ACTION).
    /// </summary>
    public ReferentialAction OnDelete { get; set; } = ReferentialAction.NoAction;

    // اختياري:
    public string? TargetProperty { get; set; }        // اسم الـ Property في الكيان الهدف
    public bool IsRequired { get; set; } = false;      // هل العلاقة إلزامية
}

public enum RelationshipType
{
    OneToOne,
    OneToMany,
    ManyToOne,
    ManyToMany
}
