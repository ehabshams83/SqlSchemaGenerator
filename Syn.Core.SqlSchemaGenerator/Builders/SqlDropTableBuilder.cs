namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Generates SQL DROP TABLE scripts for entities.
    /// </summary>
    public class SqlDropTableBuilder
    {
        /// <summary>
        /// Builds a DROP TABLE IF EXISTS SQL script for the specified table name.
        /// </summary>
        public string Build(string tableName) =>
            $"DROP TABLE IF EXISTS [{tableName}];";
    }
}