using Syn.Core.SqlSchemaGenerator.Models;

/// <summary>
/// Extension methods for converting computed column definitions to internal models.
/// </summary>
public static class ComputedColumnConverter
{
    /// <summary>
    /// Converts a <see cref="ComputedColumnDefinition"/> into a fully populated <see cref="ColumnModel"/>.
    /// </summary>
    /// <param name="comp">The computed column definition.</param>
    /// <returns>A corresponding <see cref="ColumnModel"/> instance.</returns>
    public static ColumnModel ToColumnModel(this ComputedColumnDefinition comp)
    {
        return new ColumnModel
        {
            Name = comp.Name,
            TypeName = comp.DataType,
            PropertyType = typeof(string),
            IsComputed = true,
            ComputedExpression = comp.Expression,
            ComputedSource = "Manual",
            IsPersisted = comp.IsPersisted,
            Description = comp.Description,
            IsNullable = true,
            IsUnique = false,
            Collation = null,
            DefaultValue = null,
            IgnoreReason = null,
            IsIgnored = false,
            UniqueConstraintName = null,
            Indexes = new List<IndexModel>(),
            CheckConstraints = new List<CheckConstraintModel>()
        };
    }

    /// <summary>
    /// Converts a <see cref="ColumnModel"/> into a <see cref="ComputedColumnDefinition"/>.
    /// </summary>
    /// <param name="model">The column model to convert.</param>
    /// <returns>A corresponding <see cref="ComputedColumnDefinition"/> instance.</returns>
    public static ComputedColumnDefinition ToComputedColumnDefinition(this ColumnModel model)
    {
        return new ComputedColumnDefinition
        {
            Name = model.Name,
            DataType = model.TypeName ?? "nvarchar(max)",
            Expression = model.ComputedExpression ?? string.Empty,
            IsPersisted = model.IsPersisted,
            Description = model.Description
        };
    }
}
    