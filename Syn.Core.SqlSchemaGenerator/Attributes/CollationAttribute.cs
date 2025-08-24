namespace Syn.Core.SqlSchemaGenerator.Attributes;

/// <summary>
/// Specifies the collation for the column (e.g., case sensitivity, language rules).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CollationAttribute : Attribute
{
    /// <summary>
    /// The collation name (e.g., Arabic_CI_AS).
    /// </summary>
    public string Collation { get; }

    public CollationAttribute(string collation)
    {
        Collation = collation;
    }
}