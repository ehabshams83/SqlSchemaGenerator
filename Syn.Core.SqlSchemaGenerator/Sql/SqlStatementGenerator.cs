using Syn.Core.SqlSchemaGenerator.Models;

namespace Syn.Core.SqlSchemaGenerator.Sql;

public static class SqlStatementGenerator
{
    public static string GenerateAddColumn(EntityModel entity, ColumnModel column)
    {
        return $"ALTER TABLE [{entity.Schema}].[{entity.Name}] ADD [{column.Name}] {MapType(column)}{NullableSuffix(column)};";
    }

    public static string GenerateDropColumn(EntityModel entity, ColumnModel column)
    {
        return $"ALTER TABLE [{entity.Schema}].[{entity.Name}] DROP COLUMN [{column.Name}];";
    }

    public static string GenerateAlterColumn(EntityModel entity, ColumnModel column)
    {
        return $"ALTER TABLE [{entity.Schema}].[{entity.Name}] ALTER COLUMN [{column.Name}] {MapType(column)}{NullableSuffix(column)};";
    }

    private static string MapType(ColumnModel column)
    {
        // لاحقًا نقدر نربطها بـ DialectProvider
        return column.PropertyType == typeof(string) ? "NVARCHAR(MAX)" :
               column.PropertyType == typeof(int) ? "INT" :
               column.PropertyType == typeof(bool) ? "BIT" :
               "NVARCHAR(MAX)";
    }

    private static string NullableSuffix(ColumnModel column)
        => column.IsNullable ? " NULL" : " NOT NULL";
}