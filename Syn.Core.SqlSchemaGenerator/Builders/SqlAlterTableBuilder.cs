using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Linq;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders;

/// <summary>
/// Generates ALTER TABLE SQL scripts to migrate an existing table definition
/// to match a target definition. Supports columns, indexes, PK/FK/Unique constraints,
/// and Check Constraints.
/// </summary>
public class SqlAlterTableBuilder
{
    private readonly EntityDefinitionBuilder _entityDefinitionBuilder;
    private readonly SqlTableScriptBuilder _tableScriptBuilder;

    /// <summary>
    /// Initializes a new instance using an <see cref="EntityDefinitionBuilder"/> for schema extraction.
    /// </summary>
    public SqlAlterTableBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
    {
        _entityDefinitionBuilder = entityDefinitionBuilder
            ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        _tableScriptBuilder = new SqlTableScriptBuilder(entityDefinitionBuilder);
    }

    /// <summary>
    /// Builds ALTER TABLE SQL script comparing two <see cref="EntityDefinition"/> objects.
    /// </summary>
    public string Build(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        if (oldEntity == null) throw new ArgumentNullException(nameof(oldEntity));
        if (newEntity == null) throw new ArgumentNullException(nameof(newEntity));

        // 🆕 If table doesn't exist, generate CREATE TABLE script
        if (oldEntity.Columns.Count == 0 && oldEntity.Constraints.Count == 0)
        {
            return _tableScriptBuilder.Build(newEntity);
        }

        // 🔧 Otherwise, generate ALTER TABLE script
        var sb = new StringBuilder();

        AppendColumnChanges(sb, oldEntity, newEntity);
        AppendConstraintChanges(sb, oldEntity, newEntity);
        AppendCheckConstraintChanges(sb, oldEntity, newEntity);
        AppendIndexChanges(sb, oldEntity, newEntity);
        AppendForeignKeyChanges(sb, oldEntity, newEntity);

        return sb.ToString();
    }

    /// <summary>
    /// Builds ALTER script by first generating entity definitions from Types.
    /// </summary>
    public string BuildFromTypes(Type oldType, Type newType)
    {
        if (_entityDefinitionBuilder == null)
            throw new InvalidOperationException(
                "EntityDefinitionBuilder was not provided."
            );

        var oldEntity = _entityDefinitionBuilder.Build(oldType);
        var newEntity = _entityDefinitionBuilder.Build(newType);
        return Build(oldEntity, newEntity);
    }

    #region === Columns ===

    private void AppendColumnChanges(StringBuilder sb, EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        foreach (var newCol in newEntity.Columns)
        {
            var oldCol = oldEntity.Columns
                .FirstOrDefault(c => c.Name.Equals(newCol.Name, StringComparison.OrdinalIgnoreCase));

            if (oldCol == null)
            {
                sb.AppendLine($"-- 🆕 Adding column: {newCol.Name}");
                sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD {BuildColumnDefinition(newCol)};");
            }
            else if (!ColumnsAreEquivalent(oldCol, newCol))
            {
                if (CanAlterColumnSafely(oldCol, newCol))
                {
                    sb.AppendLine($"-- 🔧 Altering column: {newCol.Name}");
                    sb.AppendLine(BuildAlterColumn(oldCol, newCol, newEntity.Name, newEntity.Schema));
                }
                else
                {
                    sb.AppendLine($"-- ⚠️ Recreating column (Drop & Add): {newCol.Name}");
                    sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP COLUMN [{newCol.Name}];");
                    sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD {BuildColumnDefinition(newCol)};");
                }
            }
        }

        foreach (var oldCol in oldEntity.Columns)
        {
            if (!newEntity.Columns.Any(c => c.Name.Equals(oldCol.Name, StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine($"-- ❌ Dropping column: {oldCol.Name}");
                sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP COLUMN [{oldCol.Name}];");
            }
        }
    }

    private bool ColumnsAreEquivalent(ColumnDefinition oldCol, ColumnDefinition newCol)
    {
        string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();

        return Normalize(oldCol.TypeName) == Normalize(newCol.TypeName)
            && oldCol.IsNullable == newCol.IsNullable
            && oldCol.IsIdentity == newCol.IsIdentity
            && string.Equals(oldCol.DefaultValue?.ToString()?.Trim(), newCol.DefaultValue?.ToString()?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private bool CanAlterColumnSafely(ColumnDefinition oldCol, ColumnDefinition newCol)
    {
        bool typeCompatible = string.Equals(oldCol.TypeName?.Trim(), newCol.TypeName?.Trim(), StringComparison.OrdinalIgnoreCase);
        bool identitySame = oldCol.IsIdentity == newCol.IsIdentity;
        return typeCompatible && identitySame;
    }


    private string BuildAlterColumn(ColumnDefinition oldCol, ColumnDefinition newCol, string tableName, string schema)
    {
        var nullable = newCol.IsNullable ? "NULL" : "NOT NULL";
        var sql = $@"
ALTER TABLE [{schema}].[{tableName}]
ALTER COLUMN [{newCol.Name}] {newCol.TypeName} {nullable};";

        if (newCol.DefaultValue != null)
        {
            sql += $@"

-- Drop old default constraint if exists
DECLARE @dfName NVARCHAR(128);
SELECT @dfName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID(N'[{schema}].[{tableName}]')
  AND c.name = '{newCol.Name}';

IF @dfName IS NOT NULL
    EXEC('ALTER TABLE [{schema}].[{tableName}] DROP CONSTRAINT [' + @dfName + ']');

ALTER TABLE [{schema}].[{tableName}]
ADD DEFAULT {newCol.DefaultValue} FOR [{newCol.Name}];";
        }

        return sql;
    }

    private bool ColumnChanged(ColumnDefinition oldCol, ColumnDefinition newCol)
    {
        return !string.Equals(oldCol.TypeName, newCol.TypeName, StringComparison.OrdinalIgnoreCase)
            || oldCol.IsNullable != newCol.IsNullable
            || oldCol.DefaultValue != newCol.DefaultValue;
    }
    private string BuildColumnDefinition(ColumnDefinition col)
    {
        var sb = new StringBuilder();
        sb.Append($"[{col.Name}] {col.TypeName}");

        if (!col.IsNullable)
            sb.Append(" NOT NULL");

        if (col.IsIdentity)
            sb.Append(" IDENTITY(1,1)");

        if (col.DefaultValue != null)
            sb.Append($" DEFAULT {HelperMethod.FormatDefaultValue(col.DefaultValue)}");

        return sb.ToString();
    }
    #endregion

    #region === PK/FK/Unique Constraints ===
    private void AppendConstraintChanges(StringBuilder sb, EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        foreach (var oldConst in oldEntity.Constraints)
        {
            var match = newEntity.Constraints.FirstOrDefault(c => c.Name == oldConst.Name);
            if (match == null || ConstraintChanged(oldConst, match))
            {
                sb.AppendLine($"-- ❌ Dropping constraint: {oldConst.Name}");
                sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldConst.Name}];");
            }
        }

        foreach (var newConst in newEntity.Constraints)
        {
            var match = oldEntity.Constraints.FirstOrDefault(c => c.Name == newConst.Name);
            if (match == null || ConstraintChanged(match, newConst))
            {
                sb.AppendLine($"-- 🆕 Adding constraint: {newConst.Name}");
                sb.AppendLine(BuildAddConstraintSql(newEntity, newConst));
            }
        }
    }

    private bool ConstraintChanged(ConstraintDefinition oldConst, ConstraintDefinition newConst)
    {
        return oldConst.Type != newConst.Type
            || !oldConst.Columns.SequenceEqual(newConst.Columns)
            || !oldConst.ReferencedColumns.SequenceEqual(newConst.ReferencedColumns)
            || !string.Equals(oldConst.ReferencedTable, newConst.ReferencedTable, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildAddConstraintSql(EntityDefinition entity, ConstraintDefinition constraint)
    {
        var cols = string.Join(", ", constraint.Columns.Select(c => $"[{c}]"));
        return constraint.Type switch
        {
            "PRIMARY KEY" => $"ALTER TABLE [{entity.Schema}].[{entity.Name}] ADD CONSTRAINT [{constraint.Name}] PRIMARY KEY ({cols});",
            "UNIQUE" => $"ALTER TABLE [{entity.Schema}].[{entity.Name}] ADD CONSTRAINT [{constraint.Name}] UNIQUE ({cols});",
            "FOREIGN KEY" => $"-- TODO: Add FOREIGN KEY definition for [{constraint.Name}]",
            _ => $"-- Unsupported constraint type: {constraint.Type} for [{constraint.Name}]"
        };
    }
    #endregion

    #region === Check Constraints ===
    private void AppendCheckConstraintChanges(StringBuilder sb, EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        foreach (var oldCheck in oldEntity.CheckConstraints)
        {
            var match = newEntity.CheckConstraints.FirstOrDefault(c => c.Name == oldCheck.Name);
            if (match == null || !string.Equals(Normalize(match.Expression), Normalize(oldCheck.Expression), StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"-- ❌ Dropping CHECK: {oldCheck.Name}");
                sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldCheck.Name}];");
            }
        }

        foreach (var newCheck in newEntity.CheckConstraints)
        {
            var match = oldEntity.CheckConstraints.FirstOrDefault(c => c.Name == newCheck.Name);
            if (match == null || !string.Equals(Normalize(match.Expression), Normalize(newCheck.Expression), StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"-- 🆕 Adding CHECK: {newCheck.Name}");
                sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD CONSTRAINT [{newCheck.Name}] CHECK ({newCheck.Expression});");
            }
        }
    }

    private string Normalize(string input) =>
        input?.Trim().Replace("(", "").Replace(")", "").Replace(" ", "") ?? string.Empty;
    #endregion

    #region === Indexes ===
    private void AppendIndexChanges(StringBuilder sb, EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        foreach (var oldIdx in oldEntity.Indexes)
        {
            if (!newEntity.Indexes.Any(i => i.Name == oldIdx.Name))
            {
                sb.AppendLine($"-- ❌ Dropping index: {oldIdx.Name}");
                sb.AppendLine($"DROP INDEX [{oldIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];");
            }
        }

        foreach (var newIdx in newEntity.Indexes)
        {
            if (!oldEntity.Indexes.Any(i => i.Name == newIdx.Name))
            {
                var cols = string.Join(", ", newIdx.Columns.Select(c => $"[{c}]"));
                var unique = newIdx.IsUnique ? "UNIQUE " : "";

                sb.AppendLine($"-- 🆕 Creating index: {newIdx.Name}");
                sb.AppendLine($"CREATE {unique}INDEX [{newIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}] ({cols});");

                if (!string.IsNullOrWhiteSpace(newIdx.Description))
                {
                    sb.AppendLine($@"
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'{newIdx.Description}',
    @level0type = N'SCHEMA',    @level0name = N'{newEntity.Schema}',
    @level1type = N'TABLE',     @level1name = N'{newEntity.Name}',
    @level2type = N'INDEX',     @level2name = N'{newIdx.Name}';");
                }
            }
        }
    }
    #endregion

    #region === Foreign Keys ===
    private void AppendForeignKeyChanges(StringBuilder sb, EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        var oldFks = oldEntity.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();
        var newFks = newEntity.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();

        // ❌ حذف العلاقات القديمة
        foreach (var oldFk in oldFks)
        {
            if (!newFks.Any(f => f.Name == oldFk.Name))
            {
                sb.AppendLine($"-- ❌ Dropping FK: {oldFk.Name}");
                sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldFk.Name}];");
            }
        }

        // 🆕 إضافة أو تعديل العلاقات الجديدة
        foreach (var newFk in newFks)
        {
            var match = oldFks.FirstOrDefault(f => f.Name == newFk.Name);
            var changed = match == null
                || !string.Equals(match.ReferencedTable, newFk.ReferencedTable, StringComparison.OrdinalIgnoreCase)
                || !match.Columns.SequenceEqual(newFk.Columns)
                || !match.ReferencedColumns.SequenceEqual(newFk.ReferencedColumns);

            if (changed)
            {
                var cols = string.Join(", ", newFk.Columns.Select(c => $"[{c}]"));
                var refCols = string.Join(", ", newFk.ReferencedColumns.Select(c => $"[{c}]"));

                sb.AppendLine($"-- 🆕 Adding FK: {newFk.Name}");
                sb.AppendLine($@"
ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}]
ADD CONSTRAINT [{newFk.Name}]
FOREIGN KEY ({cols})
REFERENCES [{newEntity.Schema}].[{newFk.ReferencedTable}] ({refCols});");
            }
        }
    }

    #endregion
}