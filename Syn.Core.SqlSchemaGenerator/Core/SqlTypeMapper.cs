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
        public static string Map(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            return underlying == typeof(int) ? "INT" :
                   underlying == typeof(string) ? "NVARCHAR(MAX)" :
                   underlying == typeof(decimal) ? "DECIMAL(18,2)" :
                   underlying == typeof(DateTime) ? "DATETIME" :
                   underlying == typeof(bool) ? "BIT" :
                   underlying == typeof(Guid) ? "UNIQUEIDENTIFIER" :
                   underlying == typeof(float) ? "REAL" :
                   underlying == typeof(double) ? "FLOAT" :
                   underlying == typeof(byte[]) ? "VARBINARY(MAX)" :
                   "NVARCHAR(MAX)"; // fallback
        }


        /// <summary>
        /// Returns the SQL type equivalent for a given .NET type name.
        /// </summary>
        public static string Map(string typeName)
        {
            return typeName switch
            {
                "Int32" or "int" => "INT",
                "String" or "string" => "NVARCHAR(MAX)",
                "Decimal" or "decimal" => "DECIMAL(18,2)",
                "DateTime" => "DATETIME",
                "Boolean" or "bool" => "BIT",
                "Guid" => "UNIQUEIDENTIFIER",
                "Single" or "float" => "REAL",
                "Double" => "FLOAT",
                "Byte[]" => "VARBINARY(MAX)",
                _ => "NVARCHAR(MAX)"
            };
        }

    }
}