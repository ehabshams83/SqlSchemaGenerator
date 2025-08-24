namespace Syn.Core.SqlSchemaGenerator.Core
{
    /// <summary>
    /// Formats .NET values into SQL-compatible string representations.
    /// </summary>
    public static class SqlValueFormatter
    {
        /// <summary>
        /// Converts a .NET value into a SQL-safe string.
        /// </summary>
        public static string Format(object value) =>
            value is string s ? $"'{s.Replace("'", "''")}'" :
            value is DateTime dt ? $"'{dt:yyyy-MM-dd HH:mm:ss}'" :
            value?.ToString() ?? "NULL";
    }
}