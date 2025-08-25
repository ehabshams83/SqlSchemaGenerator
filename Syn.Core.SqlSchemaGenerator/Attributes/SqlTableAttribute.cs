
namespace Syn.Core.SqlSchemaGenerator.Attributes;
/// <summary>
/// Custom attribute to specify SQL table name and schema.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SqlTableAttribute : Attribute
{
    /// <summary>
    /// The name of the SQL table.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The schema of the SQL table.
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTableAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the table.</param>
    /// <param name="schema">The schema of the table.</param>
    public SqlTableAttribute(string name, string schema = "dbo")
    {
        Name = name?.Trim() ?? throw new ArgumentNullException(nameof(name));
        Schema = schema?.Trim() ?? "dbo";
    }
}



