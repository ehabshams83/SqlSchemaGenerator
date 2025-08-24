namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Placeholder for generating ALTER TABLE scripts between entity versions.
    /// </summary>
    public class SqlAlterTableBuilder
    {
        /// <summary>
        /// Builds an ALTER TABLE SQL script comparing two entity types.
        /// </summary>
        public string BuildAlterScript(Type oldType, Type newType)
        {
            // Future implementation: compare old/new schema and generate ALTER statements
            return "-- ALTER TABLE logic not implemented yet";
        }
    }
}