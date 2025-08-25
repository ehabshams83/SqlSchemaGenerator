using Syn.Core.SqlSchemaGenerator.Core;
using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    [Obsolete("This class is deprecated. Please use the updated SqlTableBuilder in Syn.Core.SqlSchemaGenerator.", true)]
    /// <summary>
    /// Generates SQL CREATE TABLE scripts based on entity metadata.
    /// </summary>
    public partial class SqlTableBuilder
    {
        /// <summary>
        ///Builds a full CREATE TABLE SQL script for the specified entity type, including constraints, indexes, computed columns, and comments.
        /// </summary>
        public string Build(EntityDefinition entity)
        {
            if (entity == null || entity.IsIgnored)
                return string.Empty;

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            var sb = new StringBuilder();

            sb.AppendLine($"CREATE TABLE [{schema}].[{entity.Name}] (");

            // الأعمدة الفعلية
            foreach (var col in entity.Columns)
            {
                var line = $"  [{col.Name}] {col.TypeName}";

                if (col.Precision.HasValue && col.Scale.HasValue)
                    line += $"({col.Precision},{col.Scale})";

                if (!string.IsNullOrWhiteSpace(col.Collation))
                    line += $" COLLATE {col.Collation}";

                line += col.IsNullable ? " NULL" : " NOT NULL";

                if (col.IsIdentity)
                    line += " IDENTITY(1,1)";

                if (col.DefaultValue != null && !string.IsNullOrWhiteSpace(col.DefaultValue.ToString()))
                    line += $" DEFAULT {col.DefaultValue}";

                sb.AppendLine(line + ",");
            }

            // الأعمدة المحسوبة
            foreach (var comp in entity.ComputedColumns)
            {
                sb.AppendLine($"  [{comp.Name}] AS ({comp.Expression}),");
            }

            // المفتاح الأساسي
            if (entity.PrimaryKey?.Columns?.Count > 0)
            {
                var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"  CONSTRAINT [{entity.PrimaryKey.Name}] PRIMARY KEY ({pkCols}),");
            }

            // قيود UNIQUE
            foreach (var uc in entity.UniqueConstraints)
            {
                var cols = string.Join(", ", uc.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"  CONSTRAINT [{uc.Name}] UNIQUE ({cols}),");
            }

            // المفاتيح الأجنبية
            foreach (var fk in entity.ForeignKeys)
            {
                var fkName = !string.IsNullOrWhiteSpace(fk.ConstraintName)
                    ? fk.ConstraintName
                    : $"FK_{entity.Name}_{fk.Column}";

                var onDelete = fk.OnDelete switch
                {
                    ReferentialAction.Cascade => " ON DELETE CASCADE",
                    ReferentialAction.SetNull => " ON DELETE SET NULL",
                    ReferentialAction.SetDefault => " ON DELETE SET DEFAULT",
                    _ => ""
                };

                var onUpdate = fk.OnUpdate switch
                {
                    ReferentialAction.Cascade => " ON UPDATE CASCADE",
                    ReferentialAction.SetNull => " ON UPDATE SET NULL",
                    ReferentialAction.SetDefault => " ON UPDATE SET DEFAULT",
                    _ => ""
                };

                sb.AppendLine($"  CONSTRAINT [{fkName}] FOREIGN KEY ([{fk.Column}]) REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]){onDelete}{onUpdate},");
            }

            // إزالة آخر فاصلة
            if (sb[sb.Length - 3] == ',')
                sb.Remove(sb.Length - 3, 1);

            sb.AppendLine(");");

            // قيود CHECK
            foreach (var check in entity.CheckConstraints)
            {
                sb.AppendLine($"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{check.Name}] CHECK ({check.Expression});");
            }

            // الفهارس
            foreach (var index in entity.Indexes)
            {
                var unique = index.IsUnique ? "UNIQUE " : "";
                var cols = string.Join(", ", index.Columns.Select(c => $"[{c}]"));
                sb.AppendLine($"CREATE {unique}INDEX [{index.Name}] ON [{schema}].[{entity.Name}] ({cols});");
            }

            // وصف الجدول
            if (!string.IsNullOrWhiteSpace(entity.Description))
            {
                sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{entity.Description}', @level0type = N'SCHEMA', @level0name = N'{schema}', @level1type = N'TABLE', @level1name = N'{entity.Name}';");
            }

            // أوصاف الأعمدة
            foreach (var col in entity.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Description) || !string.IsNullOrWhiteSpace(c.Comment)))
            {
                var desc = col.Description ?? col.Comment;
                sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{desc}', @level0type = N'SCHEMA', @level0name = N'{schema}', @level1type = N'TABLE', @level1name = N'{entity.Name}', @level2type = N'COLUMN', @level2name = N'{col.Name}';");
            }

            return sb.ToString();
        }

        /// <summary>
        /// يبني سكريبت CREATE TABLE لأي كيان Type بتمريره لمُنشئ EntityDefinition.
        /// </summary>
        public string Build(Type entityType)
        {
            var builder = new EntityDefinitionBuilder();
            var entity = builder.Build(entityType);

            // إعادة استخدام الميثود الموحد
            return Build(entity);
        }

        ///// <summary>
        ///// Builds a full CREATE TABLE SQL script for the specified entity type, including constraints, indexes, computed columns, and comments.
        ///// </summary>
        //public string Build(Type entityType)
        //{
        //    var builder = new EntityDefinitionBuilder();
        //    var entity = builder.Build(entityType); // استخدام الكيان الموحد

        //    var sb = new StringBuilder();
        //    sb.AppendLine($"CREATE TABLE [{entity.Schema}].[{entity.Name}] (");

        //    // Columns
        //    foreach (var col in entity.Columns)
        //    {
        //        var line = $"  [{col.Name}] {SqlTypeMapper.Map(col.TypeName)}";

        //        if (!col.IsNullable) line += " NOT NULL";
        //        if (col.IsIdentity) line += " IDENTITY(1,1)";
        //        if (!string.IsNullOrWhiteSpace(col.DefaultValue?.ToString())) line += $" DEFAULT {col.DefaultValue}";
        //        if (!string.IsNullOrWhiteSpace(col.Collation)) line += $" COLLATE {col.Collation}";

        //        sb.AppendLine(line + ",");
        //    }

        //    // Computed Columns
        //    foreach (var comp in entity.ComputedColumns)
        //    {
        //        sb.AppendLine($"  [{comp.Name}] AS ({comp.Expression}),");
        //    }

        //    // Primary Key
        //    if (entity.PrimaryKey?.Columns?.Count > 0)
        //    {
        //        var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"  CONSTRAINT [PK_{entity.Name}] PRIMARY KEY ({pkCols}),");
        //    }

        //    // Unique Constraints
        //    foreach (var uc in entity.UniqueConstraints)
        //    {
        //        var ucCols = string.Join(", ", uc.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"  CONSTRAINT [UQ_{entity.Name}_{string.Join("_", uc.Columns)}] UNIQUE ({ucCols}),");
        //    }

        //    // Foreign Keys
        //    foreach (var fk in entity.ForeignKeys)
        //    {
        //        var onDelete = fk.OnDelete switch
        //        {
        //            ReferentialAction.Cascade => "ON DELETE CASCADE",
        //            ReferentialAction.SetNull => "ON DELETE SET NULL",
        //            ReferentialAction.SetDefault => "ON DELETE SET DEFAULT",
        //            _ => ""
        //        };

        //        var onUpdate = fk.OnUpdate switch
        //        {
        //            ReferentialAction.Cascade => "ON UPDATE CASCADE",
        //            ReferentialAction.SetNull => "ON UPDATE SET NULL",
        //            ReferentialAction.SetDefault => "ON UPDATE SET DEFAULT",
        //            _ => ""
        //        };

        //        sb.AppendLine($"  CONSTRAINT [FK_{entity.Name}_{fk.Column}] FOREIGN KEY ([{fk.Column}]) REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]) {onDelete} {onUpdate},");
        //    }

        //    // Remove trailing comma
        //    sb.Remove(sb.Length - 3, 1);
        //    sb.AppendLine(");");

        //    // Indexes
        //    foreach (var index in entity.Indexes)
        //    {
        //        var unique = index.IsUnique ? "UNIQUE " : "";
        //        var cols = string.Join(", ", index.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"CREATE {unique}INDEX [{index.Name}] ON [{entity.Schema}].[{entity.Name}] ({cols});");
        //    }

        //    // Comments
        //    foreach (var col in entity.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Comment)))
        //    {
        //        sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{col.Comment}',");
        //        sb.AppendLine($"  @level0type = N'SCHEMA', @level0name = N'{entity.Schema}',");
        //        sb.AppendLine($"  @level1type = N'TABLE',  @level1name = N'{entity.Name}',");
        //        sb.AppendLine($"  @level2type = N'COLUMN', @level2name = N'{col.Name}';");
        //    }

        //    return sb.ToString();
        //}


        ///// <summary>
        ///// Builds a CREATE TABLE SQL script for the specified <see cref="EntityDefinition"/>, including indexes.
        ///// </summary>
        //public string Build(EntityDefinition entity)
        //{
        //    if (entity == null || entity.IsIgnored)
        //        return string.Empty;

        //    var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
        //    var sb = new StringBuilder();

        //    sb.AppendLine($"CREATE TABLE [{schema}].[{entity.Name}] (");

        //    // Physical columns
        //    foreach (var col in entity.Columns)
        //    {
        //        var line = $"  [{col.Name}] {col.TypeName}";

        //        if (col.Precision.HasValue && col.Scale.HasValue)
        //            line += $"({col.Precision},{col.Scale})";

        //        if (!string.IsNullOrWhiteSpace(col.Collation))
        //            line += $" COLLATE {col.Collation}";

        //        line += col.IsNullable ? " NULL" : " NOT NULL";

        //        if (col.IsIdentity)
        //            line += " IDENTITY(1,1)";

        //        if (col.DefaultValue != null && !string.IsNullOrWhiteSpace(col.DefaultValue.ToString()))
        //            line += $" DEFAULT {col.DefaultValue}";

        //        sb.AppendLine(line + ",");
        //    }

        //    // Computed columns
        //    foreach (var comp in entity.ComputedColumns)
        //    {
        //        sb.AppendLine($"  [{comp.Name}] AS ({comp.Expression}),");
        //    }

        //    // Primary Key
        //    if (entity.PrimaryKey?.Columns?.Count > 0)
        //    {
        //        var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"  CONSTRAINT [{entity.PrimaryKey.Name}] PRIMARY KEY ({pkCols}),");
        //    }

        //    // Unique Constraints
        //    foreach (var uc in entity.UniqueConstraints)
        //    {
        //        var cols = string.Join(", ", uc.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"  CONSTRAINT [{uc.Name}] UNIQUE ({cols}),");
        //    }

        //    // Foreign Keys
        //    foreach (var fk in entity.ForeignKeys)
        //    {
        //        var fkName = !string.IsNullOrWhiteSpace(fk.ConstraintName)
        //            ? fk.ConstraintName
        //            : $"FK_{entity.Name}_{fk.Column}";

        //        var onDelete = fk.OnDelete switch
        //        {
        //            ReferentialAction.Cascade => " ON DELETE CASCADE",
        //            ReferentialAction.SetNull => " ON DELETE SET NULL",
        //            ReferentialAction.SetDefault => " ON DELETE SET DEFAULT",
        //            _ => ""
        //        };

        //        var onUpdate = fk.OnUpdate switch
        //        {
        //            ReferentialAction.Cascade => " ON UPDATE CASCADE",
        //            ReferentialAction.SetNull => " ON UPDATE SET NULL",
        //            ReferentialAction.SetDefault => " ON UPDATE SET DEFAULT",
        //            _ => ""
        //        };

        //        sb.AppendLine($"  CONSTRAINT [{fkName}] FOREIGN KEY ([{fk.Column}]) REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]){onDelete}{onUpdate},");
        //    }

        //    // إزالة آخر فاصلة
        //    if (sb[sb.Length - 3] == ',')
        //        sb.Remove(sb.Length - 3, 1);

        //    sb.AppendLine(");");

        //    // Check constraints
        //    foreach (var check in entity.CheckConstraints)
        //    {
        //        sb.AppendLine($"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{check.Name}] CHECK ({check.Expression});");
        //    }

        //    // Indexes
        //    foreach (var index in entity.Indexes)
        //    {
        //        var unique = index.IsUnique ? "UNIQUE " : "";
        //        var cols = string.Join(", ", index.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"CREATE {unique}INDEX [{index.Name}] ON [{schema}].[{entity.Name}] ({cols});");
        //    }

        //    // Table description
        //    if (!string.IsNullOrWhiteSpace(entity.Description))
        //    {
        //        sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{entity.Description}', @level0type = N'SCHEMA', @level0name = N'{schema}', @level1type = N'TABLE', @level1name = N'{entity.Name}';");
        //    }

        //    // Column descriptions
        //    foreach (var col in entity.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Description) || !string.IsNullOrWhiteSpace(c.Comment)))
        //    {
        //        var desc = col.Description ?? col.Comment;
        //        sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{desc}', @level0type = N'SCHEMA', @level0name = N'{schema}', @level1type = N'TABLE', @level1name = N'{entity.Name}', @level2type = N'COLUMN', @level2name = N'{col.Name}';");
        //    }

        //    return sb.ToString();
        //}

        //public string Build(EntityDefinition entity)
        //{
        //    if (entity.IsIgnored)
        //        return string.Empty;

        //    var sb = new StringBuilder();
        //    var schema = entity.Schema ?? "dbo";

        //    sb.AppendLine($"CREATE TABLE [{schema}].[{entity.Name}] (");

        //    // Physical Columns
        //    for (int i = 0; i < entity.Columns.Count; i++)
        //    {
        //        var col = entity.Columns[i];
        //        var line = $"    [{col.Name}] {col.TypeName}";

        //        if (col.Precision.HasValue && col.Scale.HasValue)
        //            line += $"({col.Precision},{col.Scale})";

        //        if (!string.IsNullOrWhiteSpace(col.Collation))
        //            line += $" COLLATE {col.Collation}";

        //        line += col.IsNullable ? " NULL" : " NOT NULL";

        //        if (col.IsIdentity)
        //            line += " IDENTITY(1,1)";

        //        if (!string.IsNullOrWhiteSpace(col.DefaultValue?.ToString()))
        //            line += $" DEFAULT {col.DefaultValue}";

        //        sb.AppendLine(i < entity.Columns.Count - 1 || entity.ComputedColumns.Any() ? line + "," : line);
        //    }

        //    // Computed Columns
        //    for (int i = 0; i < entity.ComputedColumns.Count; i++)
        //    {
        //        var comp = entity.ComputedColumns[i];
        //        var line = $"    [{comp.Name}] AS ({comp.Expression})";
        //        sb.AppendLine(i < entity.ComputedColumns.Count - 1 ? line + "," : line);
        //    }

        //    sb.AppendLine(");");

        //    // Primary Key
        //    if (entity.PrimaryKey?.Columns?.Any() == true)
        //    {
        //        sb.AppendLine($"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{entity.PrimaryKey.Name}] PRIMARY KEY ({string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"))});");
        //    }

        //    // Unique Constraints
        //    foreach (var uc in entity.UniqueConstraints)
        //    {
        //        sb.AppendLine($"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{uc.Name}] UNIQUE ({string.Join(", ", uc.Columns.Select(c => $"[{c}]"))});");
        //    }

        //    // Foreign Keys
        //    foreach (var fk in entity.ForeignKeys)
        //    {
        //        sb.AppendLine($"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{fk.}] FOREIGN KEY ({string.Join(", ", fk.Columns.Select(c => $"[{c}]"))}) REFERENCES [{fk.ReferenceSchema ?? "dbo"}].[{fk.ReferenceTable}] ({string.Join(", ", fk.ReferenceColumns.Select(c => $"[{c}]"))}) ON DELETE {fk.OnDelete} ON UPDATE {fk.OnUpdate};");
        //    }

        //    // Check Constraints
        //    foreach (var check in entity.CheckConstraints)
        //    {
        //        sb.AppendLine($"ALTER TABLE [{schema}].[{entity.Name}] ADD CONSTRAINT [{check.Name}] CHECK ({check.Expression});");
        //    }

        //    // Indexes
        //    foreach (var index in entity.Indexes)
        //    {
        //        sb.AppendLine($"CREATE {(index.IsUnique ? "UNIQUE " : "")}INDEX [{index.Name}] ON [{schema}].[{entity.Name}] ({string.Join(", ", index.Columns.Select(c => $"[{c}]"))});");
        //    }

        //    // Description (Extended Property)
        //    if (!string.IsNullOrWhiteSpace(entity.Description))
        //    {
        //        sb.AppendLine($"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{entity.Description}', @level0type = N'SCHEMA', @level0name = N'{schema}', @level1type = N'TABLE', @level1name = N'{entity.Name}';");
        //    }

        //    return sb.ToString();
        //}



        //public string Build(EntityDefinition entity)
        //{
        //    var sb = new StringBuilder();
        //    sb.AppendLine($"CREATE TABLE [{entity.Schema}].[{entity.Name}] (");

        //    // Columns
        //    foreach (var col in entity.Columns)
        //    {
        //        var line = $"  [{col.Name}] {SqlTypeMapper.Map(col.TypeName)}";

        //        if (!col.IsNullable) line += " NOT NULL";
        //        if (!string.IsNullOrWhiteSpace(col.DefaultValue?.ToString())) line += $" DEFAULT {col.DefaultValue}";

        //        sb.AppendLine(line + ",");
        //    }

        //    // Computed Columns
        //    foreach (var comp in entity.ComputedColumns)
        //    {
        //        sb.AppendLine($"  [{comp.Name}] AS ({comp.Expression}),");
        //    }

        //    // Primary Key
        //    if (entity.PrimaryKey?.Columns?.Count > 0)
        //    {
        //        var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"  CONSTRAINT [PK_{entity.Name}] PRIMARY KEY ({pkCols}),");
        //    }

        //    // Unique Constraints
        //    foreach (var uc in entity.UniqueConstraints)
        //    {
        //        var ucCols = string.Join(", ", uc.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"  CONSTRAINT [UQ_{entity.Name}_{string.Join("_", uc.Columns)}] UNIQUE ({ucCols}),");
        //    }

        //    // Foreign Keys
        //    foreach (var fk in entity.ForeignKeys)
        //    {
        //        sb.AppendLine($"  CONSTRAINT [FK_{entity.Name}_{fk.Column}] FOREIGN KEY ([{fk.Column}]) REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]),");
        //    }

        //    // Remove trailing comma
        //    sb.Remove(sb.Length - 3, 1);
        //    sb.AppendLine(");");

        //    // Indexes (outside CREATE TABLE)
        //    foreach (var index in entity.Indexes)
        //    {
        //        var unique = index.IsUnique ? "UNIQUE " : "";
        //        var cols = string.Join(", ", index.Columns.Select(c => $"[{c}]"));
        //        sb.AppendLine($"CREATE {unique}INDEX [{index.Name}] ON [{entity.Schema}].[{entity.Name}] ({cols});");
        //    }

        //    return sb.ToString();
        //}

    }
}