using Syn.Core.SqlSchemaGenerator.Models;

using System.Reflection;


namespace Syn.Core.SqlSchemaGenerator.AttributeHandlers;

/// <summary>
/// Defines a contract for handling custom schema attributes.
/// Each handler inspects a property and modifies the column metadata accordingly.
/// </summary>
public interface ISchemaAttributeHandler
{
    /// <summary>
    /// Applies attribute logic to the given property and updates the column model.
    /// </summary>
    /// <param name="property">The reflected property to inspect.</param>
    /// <param name="column">The column model to populate or modify.</param>
    void Apply(PropertyInfo property, ColumnModel column);
}