using Syn.Core.SqlSchemaGenerator.Migrations.Steps;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Sql;

/// <summary>
/// Generates SQL statements from migration steps.
/// </summary>
public class SqlMigrationGenerator
{
    /// <summary>
    /// Generates SQL script for a given migration step.
    /// </summary>
    /// <param name="step">The migration step to process.</param>
    /// <returns>SQL statement as string.</returns>
    public string GenerateSql(MigrationStep step)
    {
        return step.Operation switch
        {
            MigrationOperation.CreateEntity => GenerateCreateEntity(step),
            MigrationOperation.DropEntity => GenerateDropEntity(step),
            MigrationOperation.AddColumn => GenerateAddColumn(step),
            MigrationOperation.DropColumn => GenerateDropColumn(step),
            MigrationOperation.AlterColumn => GenerateAlterColumn(step),
            MigrationOperation.AddIndex => GenerateAddIndex(step),
            MigrationOperation.DropIndex => GenerateDropIndex(step),
            MigrationOperation.AlterIndex => GenerateAlterIndex(step),
            MigrationOperation.AddConstraint => GenerateAddConstraint(step),
            MigrationOperation.DropConstraint => GenerateDropConstraint(step),
            _ => $"-- Unsupported operation: {step.Operation}"
        };
    }

    /// <summary>
    /// Generates SQL for creating a new entity (table).
    /// </summary>
    private string GenerateCreateEntity(MigrationStep step)
    {
        if (step.Metadata?["Entity"] is not EntityModel entity)
            return "-- Invalid entity metadata";

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE [{entity.Schema}].[{entity.Name}] (");

        var columnDefs = entity.Columns.Select(c => "    " + BuildColumnDefinition(c));
        sb.AppendLine(string.Join(",\n", columnDefs));
        sb.AppendLine(");");

        foreach (var constraint in entity.Constraints)
        {
            sb.AppendLine(GenerateAddConstraint(new MigrationStep
            {
                Operation = MigrationOperation.AddConstraint,
                Schema = entity.Schema,
                EntityName = entity.Name,
                Metadata = new Dictionary<string, object> { ["Constraint"] = constraint }
            }));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates SQL for dropping an entity (table).
    /// </summary>
    private string GenerateDropEntity(MigrationStep step)
    {
        return $"DROP TABLE [{step.Schema}].[{step.EntityName}];";
    }

    /// <summary>
    /// Generates SQL for adding a column.
    /// </summary>
    private string GenerateAddColumn(MigrationStep step)
    {
        if (step.Metadata?["Column"] is not ColumnModel column)
            return "-- Invalid column metadata";

        return $"ALTER TABLE [{step.Schema}].[{step.EntityName}] ADD {BuildColumnDefinition(column)};";
    }

    /// <summary>
    /// Generates SQL for dropping a column.
    /// </summary>
    private string GenerateDropColumn(MigrationStep step)
    {
        if (step.Metadata?["Column"] is not ColumnModel column)
            return "-- Invalid column metadata";

        return $"ALTER TABLE [{step.Schema}].[{step.EntityName}] DROP COLUMN [{column.Name}];";
    }

    /// <summary>
    /// Generates SQL for altering a column.
    /// </summary>
    private string GenerateAlterColumn(MigrationStep step)
    {
        if (step.Metadata?["NewColumn"] is not ColumnModel column)
            return "-- Invalid column metadata";

        return $"ALTER TABLE [{step.Schema}].[{step.EntityName}] ALTER COLUMN {BuildColumnDefinition(column)};";
    }

    /// <summary>
    /// Generates SQL for adding an index.
    /// </summary>
    private string GenerateAddIndex(MigrationStep step)
    {
        if (step.Metadata?["Index"] is not IndexModel index)
            return "-- Invalid index metadata";

        var unique = index.IsUnique ? "UNIQUE " : "";
        var columns = string.Join(", ", index.Columns.Select(c => $"[{c}]"));

        return $"CREATE {unique}INDEX [{index.Name}] ON [{step.Schema}].[{step.EntityName}] ({columns});";
    }

    /// <summary>
    /// Generates SQL for dropping an index.
    /// </summary>
    private string GenerateDropIndex(MigrationStep step)
    {
        if (step.Metadata?["Index"] is not IndexModel index)
            return "-- Invalid index metadata";

        return $"DROP INDEX [{index.Name}] ON [{step.Schema}].[{step.EntityName}];";
    }

    /// <summary>
    /// Generates SQL for altering an index (drop + re-create).
    /// </summary>
    private string GenerateAlterIndex(MigrationStep step)
    {
        var drop = GenerateDropIndex(step);
        var add = GenerateAddIndex(new MigrationStep
        {
            Operation = MigrationOperation.AddIndex,
            EntityName = step.EntityName,
            Schema = step.Schema,
            Metadata = new Dictionary<string, object>
            {
                ["Index"] = step.Metadata?["NewIndex"]
            }
        });

        return $"{drop}\n{add}";
    }

    /// <summary>
    /// Generates SQL for adding a constraint.
    /// </summary>
    private string GenerateAddConstraint(MigrationStep step)
    {
        if (step.Metadata == null || !step.Metadata.TryGetValue("Constraint", out var obj) || obj is not ConstraintModel constraint)
            return "-- Invalid constraint metadata";


        var table = $"[{step.Schema}].[{step.EntityName}]";

        return constraint.Type switch
        {
            ConstraintType.Check =>
                $"ALTER TABLE {table} ADD CONSTRAINT [{constraint.Name}] CHECK ({constraint.Expression});",

            ConstraintType.Unique =>
                $"ALTER TABLE {table} ADD CONSTRAINT [{constraint.Name}] UNIQUE ({string.Join(", ", constraint.Columns.Select(c => $"[{c}]"))});",

            ConstraintType.PrimaryKey =>
                $"ALTER TABLE {table} ADD CONSTRAINT [{constraint.Name}] PRIMARY KEY ({string.Join(", ", constraint.Columns.Select(c => $"[{c}]"))});",

            ConstraintType.ForeignKey => GenerateForeignKeyConstraint(table, constraint),

            _ => $"-- Unsupported constraint type: {constraint.Type}"
        };
    }

    /// <summary>
    /// Generates SQL for dropping a constraint.
    /// </summary>
    private string GenerateDropConstraint(MigrationStep step)
    {
        if (step.Metadata == null || !step.Metadata.TryGetValue("ConstraintName", out var obj) || obj is not ConstraintModel constraint)
            return "-- Invalid constraint metadata";


        return $"ALTER TABLE [{step.Schema}].[{step.EntityName}] DROP CONSTRAINT [{constraint.Name}];";
    }

    /// <summary>
    /// Generates SQL for a foreign key constraint.
    /// </summary>
    private string GenerateForeignKeyConstraint(string table, ConstraintModel constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint.ForeignKeyTargetTable) || constraint.ForeignKeyTargetColumns == null)
            return "-- Incomplete foreign key metadata";

        var sourceCols = string.Join(", ", constraint.Columns.Select(c => $"[{c}]"));
        var targetCols = string.Join(", ", constraint.ForeignKeyTargetColumns.Select(c => $"[{c}]"));

        return $@"ALTER TABLE {table} ADD CONSTRAINT [{constraint.Name}] FOREIGN KEY ({sourceCols})
REFERENCES [{constraint.ForeignKeyTargetTable}] ({targetCols});";
    }

    /// <summary>
    /// Builds SQL definition for a column.
    /// </summary>
    private string BuildColumnDefinition(ColumnModel column)
    {
        if (column.IsComputed)
        {
            var persisted = column.IsPersisted ? " PERSISTED" : "";
            return $"[{column.Name}] AS {column.ComputedExpression}{persisted}";
        }

        var sb = new StringBuilder();
        sb.Append($"[{column.Name}] {BuildType(column)}");
        sb.Append(column.IsNullable ? " NULL" : " NOT NULL");

        if (column.DefaultValue is not null)
            sb.Append($" DEFAULT {FormatDefaultValue(column.DefaultValue)}");

        if (!string.IsNullOrWhiteSpace(column.Collation))
            sb.Append($" COLLATE {column.Collation}");

        return sb.ToString();
    }

    /// <summary>
    /// Builds SQL type string for a column.
    /// </summary>
    private string BuildType(ColumnModel column)
    {
        if (column.TypeName.StartsWith("nvarchar") && column.MaxLength > 0)
            return $"nvarchar({column.MaxLength})";

        return column.TypeName;
    }

    /// <summary>
    /// Formats default value for SQL.
    /// </summary>
    private string FormatDefaultValue(object value)
    {
        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            _ => value.ToString() ?? "NULL"
        };
    }
}