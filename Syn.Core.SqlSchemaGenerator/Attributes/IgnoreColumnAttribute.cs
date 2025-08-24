namespace Syn.Core.SqlSchemaGenerator.Attributes;

/// <summary>
/// Marks the column to be ignored during schema generation.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class IgnoreColumnAttribute : Attribute
{
    // Marker only — no properties needed.
}