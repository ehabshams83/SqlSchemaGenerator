namespace Syn.Core.SqlSchemaGenerator.Core
{
    /// <summary>
    /// Maps .NET types to their corresponding SQL data types.
    /// </summary>
    public static class SqlTypeMapper
    {
        /// <summary>
        /// Returns the SQL type equivalent for a given .NET type.
        /// </summary>
        public static string Map(Type type) =>
            type == typeof(int) ? "INT" :
            type == typeof(string) ? "NVARCHAR(MAX)" :
            type == typeof(decimal) ? "DECIMAL(18,2)" :
            type == typeof(DateTime) ? "DATETIME" :
            "NVARCHAR(MAX)";
    }
}