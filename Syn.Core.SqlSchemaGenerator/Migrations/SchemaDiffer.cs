using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Sql;

namespace Syn.Core.SqlSchemaGenerator.Migrations;

public static class SchemaDiffer
{
    public static SqlMigrationScript Diff(EntityModel oldModel, EntityModel newModel)
    {
        var statements = new List<string>();

        foreach (var newCol in newModel.Columns)
        {
            var oldCol = oldModel.Columns.FirstOrDefault(c => c.Name == newCol.Name);
            if (oldCol == null)
            {
                statements.Add(SqlStatementGenerator.GenerateAddColumn(newModel, newCol));
                continue;
            }

            if (!AreColumnsEqual(oldCol, newCol))
            {
                statements.Add(SqlStatementGenerator.GenerateAlterColumn(newModel, newCol));
            }
        }

        foreach (var oldCol in oldModel.Columns)
        {
            if (!newModel.Columns.Any(c => c.Name == oldCol.Name))
            {
                statements.Add(SqlStatementGenerator.GenerateDropColumn(newModel, oldCol));
            }
        }

        var sql = string.Join(Environment.NewLine, statements);

        return new SqlMigrationScript
        {
            EntityName = newModel.Name,
            Version = newModel.Version,
            Sql = sql
        };
    }

    private static bool AreColumnsEqual(ColumnModel a, ColumnModel b)
    {
        return a.PropertyType == b.PropertyType &&
               a.IsNullable == b.IsNullable &&
               a.DefaultValue?.ToString() == b.DefaultValue?.ToString() &&
               a.Collation == b.Collation &&
               a.IsComputed == b.IsComputed &&
               a.ComputedExpression == b.ComputedExpression;
    }
}