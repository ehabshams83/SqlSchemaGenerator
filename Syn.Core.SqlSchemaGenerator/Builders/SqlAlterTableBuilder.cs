using Microsoft.Data.SqlClient;

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
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance using an <see cref="EntityDefinitionBuilder"/> for schema extraction.
    /// </summary>
    public SqlAlterTableBuilder(EntityDefinitionBuilder entityDefinitionBuilder, string connectionString)
    {
        _entityDefinitionBuilder = entityDefinitionBuilder
            ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
        _tableScriptBuilder = new SqlTableScriptBuilder(entityDefinitionBuilder);
        _connectionString = connectionString;
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
                if (CanAlterColumn(oldCol, newCol, newEntity.Schema, newEntity.Name))
                {
                    sb.AppendLine($"-- 🔧 Altering column: {newCol.Name}");
                    sb.AppendLine(BuildAlterColumn(oldCol, newCol, newEntity.Name, newEntity.Schema, newEntity));
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

    /// <summary>
    /// Compares two column definitions to determine if they are equivalent.
    /// Now considers text length differences (e.g., nvarchar(max) → nvarchar(600)) as non-equivalent.
    /// </summary>
    private bool ColumnsAreEquivalent(ColumnDefinition oldCol, ColumnDefinition newCol)
    {
        string baseOldType = oldCol.TypeName.Split('(')[0].Trim().ToLowerInvariant();
        string baseNewType = newCol.TypeName.Split('(')[0].Trim().ToLowerInvariant();

        // النوع الأساسي لازم يكون نفسه
        if (!string.Equals(baseOldType, baseNewType, StringComparison.OrdinalIgnoreCase))
            return false;

        // نفس الـ Identity
        if (oldCol.IsIdentity != newCol.IsIdentity)
            return false;

        // نفس الـ Nullable
        if (oldCol.IsNullable != newCol.IsNullable)
            return false;

        // نفس الـ DefaultValue
        if (!string.Equals(oldCol.DefaultValue?.ToString()?.Trim(),
                           newCol.DefaultValue?.ToString()?.Trim(),
                           StringComparison.OrdinalIgnoreCase))
            return false;

        // 🆕 فحص فرق الطول لو النوع نصي
        if (IsTextType(baseOldType))
        {
            int oldLen = ExtractLengthForIndex(oldCol.TypeName); // -1 = max
            int newLen = ExtractLengthForIndex(newCol.TypeName);

            // لو الطول مختلف (بما في ذلك max → رقم أو رقم → max) نعتبرهم غير متساويين
            if (oldLen != newLen)
                return false;
        }

        // لو النوع رقمي، ممكن تضيف فحص Precision/Scale لو حابب
        return true;
    }



    /// <summary>
    /// Builds an ALTER COLUMN SQL statement with safety checks and smart length adjustments.
    /// </summary>

    private string BuildAlterColumn(ColumnDefinition oldCol, ColumnDefinition newCol, string tableName, string schema, EntityDefinition newEntity)
    {
        // 🛡️ فحص الأمان لتغيير Identity
        if (oldCol.IsIdentity != newCol.IsIdentity && !IsTableEmpty(schema, tableName))
        {
            WarnOnce($"{schema}.{tableName}.{newCol.Name}.Identity",
                $"⚠️ [ALTER] Skipped {schema}.{tableName}.{newCol.Name} → Identity change unsafe (table has data)");
            return $"-- Skipped ALTER COLUMN for {newCol.Name} due to Identity change on non-empty table";
        }

        // 🛡️ فحص الأمان لتغيير NOT NULL
        if (!newCol.IsNullable && oldCol.IsNullable)
        {
            int nullCount = ColumnNullCount(schema, tableName, newCol.Name);
            Console.WriteLine($"[TRACE:NullCheck] {schema}.{tableName}.{newCol.Name} → Found {nullCount} NULL values");
            if (nullCount > 0)
            {
                WarnOnce($"{schema}.{tableName}.{newCol.Name}.NotNull",
                    $"⚠️ [ALTER] Skipped {schema}.{tableName}.{newCol.Name} → NOT NULL change unsafe (NULL values exist)");
                return $"-- Skipped ALTER COLUMN for {newCol.Name} due to NULL values in column";
            }
        }
        else if (newCol.IsNullable && !oldCol.IsNullable)
        {
            Console.WriteLine($"[TRACE:NullabilityChange] {schema}.{tableName}.{newCol.Name} → Changed to allow NULL values (safe change)");
        }

        // 🛡️ فحص النوع والطول مع منطق Smart Length Fix
        if (IsTextType(oldCol.TypeName))
        {
            int oldLen = ExtractLengthForIndex(oldCol.TypeName); // -1 = max
            int newLen = ExtractLengthForIndex(newCol.TypeName);

            // لو DB فيها max والكود محدد طول
            if (IsMaxType(oldCol.TypeName) && newLen > 0)
            {
                newCol.TypeName = $"nvarchar({newLen})";

                // 🆕 فحص الفهارس المركبة اللي العمود ده جزء منها
                if (newCol.Indexes != null && newCol.Indexes.Count > 0)
                {
                    foreach (var idx in newCol.Indexes.ToList())
                    {
                        int totalBytes = 0;
                        var colSizes = new List<string>();

                        foreach (var colName in idx.Columns)
                        {
                            var colDef = newEntity.Columns
                                .FirstOrDefault(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                            if (colDef != null)
                            {
                                int colBytes = GetColumnMaxLength(colDef.TypeName);

                                if (IsMaxType(colDef.TypeName))
                                    colBytes = GetColumnMaxLength("nvarchar(450)");

                                totalBytes += colBytes;
                                colSizes.Add($"{colDef.Name}={colBytes}B");
                            }
                        }

                        if (totalBytes > 900)
                        {
                            WarnOnce($"{schema}.{tableName}.IDX:{idx.Name}",
                                $"[WARN] {schema}.{tableName} index [{idx.Name}] skipped — total key size {totalBytes} bytes exceeds 900. Columns: {string.Join(", ", colSizes)}");
                            newCol.Indexes.Remove(idx);
                        }
                    }
                }

                if ((newLen * 2) > 900)
                {
                    WarnOnce($"{schema}.{tableName}.{newCol.Name}.Length",
                        $"[WARN] {schema}.{tableName}.{newCol.Name} length {newLen} may exceed index key size limit — index creation skipped, but column length will be updated.");
                    newCol.Indexes?.Clear();
                }
                else
                {
                    Console.WriteLine($"[AUTO-FIX] Changing {schema}.{tableName}.{newCol.Name} from {oldCol.TypeName} to {newCol.TypeName} based on model attribute.");
                }
            }
            // لو DB فيها max ومفيش طول محدد لكن عليه Index
            else if (IsMaxType(oldCol.TypeName) && newLen == -1 && newCol.Indexes != null && newCol.Indexes.Count > 0)
            {
                int safeLength = 450;
                Console.WriteLine($"[AUTO-FIX] Changing {schema}.{tableName}.{newCol.Name} from nvarchar(max) to nvarchar({safeLength}) for indexing safety.");
                newCol.TypeName = $"nvarchar({safeLength})";
            }
            // لو DB فيها max ومفيش طول محدد ومفيش Index
            else if (IsMaxType(oldCol.TypeName) && IsMaxType(newCol.TypeName))
            {
                int safeLength = 450;
                Console.WriteLine($"[AUTO-FIX] Changing {schema}.{tableName}.{newCol.Name} from nvarchar(max) to nvarchar({safeLength}) to match schema standards.");
                newCol.TypeName = $"nvarchar({safeLength})";
            }
            else if (oldLen > newLen && newLen > 0)
            {
                Console.WriteLine($"[TRACE:TypeChange] Reducing length of {schema}.{tableName}.{newCol.Name} from {oldCol.TypeName} to {newCol.TypeName}");
            }
            else if (oldLen != newLen && newLen > 0)
            {
                Console.WriteLine($"[TRACE:TypeChange] Adjusting length of {schema}.{tableName}.{newCol.Name} from {oldCol.TypeName} to {newCol.TypeName}");
            }
        }
        else if (!oldCol.TypeName.Equals(newCol.TypeName, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[TRACE:TypeChange] {schema}.{tableName}.{newCol.Name} → Type change from {oldCol.TypeName} to {newCol.TypeName}");
        }

        // 🛠️ توليد أمر ALTER COLUMN
        var nullable = newCol.IsNullable ? "NULL" : "NOT NULL";
        var sql = $@"
ALTER TABLE [{schema}].[{tableName}]
ALTER COLUMN [{newCol.Name}] {newCol.TypeName} {nullable};";

        // 🛠️ لو فيه Default Value جديدة
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

    // Runner
    private static readonly HashSet<string> _warnedKeys = new(StringComparer.OrdinalIgnoreCase);

    private bool WarnOnce(string key, string message)
    {
        if (_warnedKeys.Contains(key))
        {
            HelperMethod._suppressedWarnings.Add($"[{key}]");
            return false;
        }

        Console.WriteLine(message);
        _warnedKeys.Add(key);
        return true;
    }



    /// <summary>
    /// Checks if the SQL type is a text-based type.
    /// </summary>
    private bool IsTextType(string typeName) =>
        typeName.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase) ||
        typeName.StartsWith("varchar", StringComparison.OrdinalIgnoreCase) ||
        typeName.StartsWith("nchar", StringComparison.OrdinalIgnoreCase) ||
        typeName.StartsWith("char", StringComparison.OrdinalIgnoreCase);

    //// 📊 استخراج الطول من النص (nvarchar(600) → 600, nvarchar(max) → -1)
    //private int ExtractLength(string typeName)
    //{
    //    var start = typeName.IndexOf('(');
    //    var end = typeName.IndexOf(')');
    //    if (start > 0 && end > start)
    //    {
    //        var numStr = typeName.Substring(start + 1, end - start - 1);
    //        if (numStr.Equals("max", StringComparison.OrdinalIgnoreCase))
    //            return -1;
    //        if (int.TryParse(numStr, out int len))
    //            return len;
    //    }
    //    return 0; // لو مفيش طول محدد
    //}


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
                // ✅ فحص أمان للـ PRIMARY KEY
                if (oldConst.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                {
                    if (!CanAlterPrimaryKey(oldEntity, newEntity))
                    {
                        sb.AppendLine($"-- ⚠️ Skipped dropping PRIMARY KEY {oldConst.Name} due to safety check");
                        continue;
                    }
                }

                sb.AppendLine($"-- ❌ Dropping constraint: {oldConst.Name}");
                sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldConst.Name}];");
            }
        }

        foreach (var newConst in newEntity.Constraints)
        {
            var match = oldEntity.Constraints.FirstOrDefault(c => c.Name == newConst.Name);
            if (match == null || ConstraintChanged(match, newConst))
            {
                // ✅ فحص أمان للـ PRIMARY KEY قبل الإضافة
                if (newConst.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                {
                    if (!CanAlterPrimaryKey(oldEntity, newEntity))
                    {
                        sb.AppendLine($"-- ⚠️ Skipped adding PRIMARY KEY {newConst.Name} due to safety check");
                        continue;
                    }
                }

                sb.AppendLine($"-- 🆕 Adding constraint: {newConst.Name}");
                sb.AppendLine(BuildAddConstraintSql(newEntity, newConst));
            }
        }
    }

    /// <summary>
    /// Determines if a constraint has changed in a meaningful way.
    /// Ignores differences in name casing or column order.
    /// </summary>
    private bool ConstraintChanged(ConstraintDefinition oldConst, ConstraintDefinition newConst)
    {
        // لو نوع القيد نفسه اتغير → تغيير جوهري
        if (!string.Equals(oldConst.Type, newConst.Type, StringComparison.OrdinalIgnoreCase))
            return true;

        // قارن الأعمدة بدون حساسية Case وبغض النظر عن الترتيب
        var oldCols = oldConst.Columns
            .Select(c => c.Trim().ToLowerInvariant())
            .OrderBy(c => c)
            .ToList();

        var newCols = newConst.Columns
            .Select(c => c.Trim().ToLowerInvariant())
            .OrderBy(c => c)
            .ToList();

        if (!oldCols.SequenceEqual(newCols))
            return true;

        // لو القيد من نوع FOREIGN KEY، قارن الأعمدة المرجعية بنفس الطريقة
        if (string.Equals(oldConst.Type, "FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
        {
            var oldRefCols = oldConst.ReferencedColumns
                .Select(c => c.Trim().ToLowerInvariant())
                .OrderBy(c => c)
                .ToList();

            var newRefCols = newConst.ReferencedColumns
                .Select(c => c.Trim().ToLowerInvariant())
                .OrderBy(c => c)
                .ToList();

            if (!oldRefCols.SequenceEqual(newRefCols))
                return true;

            // قارن اسم الجدول المرجعي بدون حساسية Case
            if (!string.Equals(oldConst.ReferencedTable, newConst.ReferencedTable, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // لو وصلنا هنا → مفيش تغيير جوهري
        return false;
    }

    private string BuildAddConstraintSql(EntityDefinition entity, ConstraintDefinition constraint)
    {
        var cols = string.Join(", ", constraint.Columns.Select(c => $"[{c}]"));

        // فحص خاص بالـ PRIMARY KEY
        if (constraint.Type.Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
        {
            // لو الجدول مش فاضى وكان التغيير على الـ Identity بس → نتجنب الإضافة
            if (!IsTableEmpty(entity.Schema, entity.Name))
            {
                Console.WriteLine($"⚠️ Skipped adding PRIMARY KEY [{constraint.Name}] on {entity.Schema}.{entity.Name} because table has data and Identity change is unsafe.");
                return $"-- Skipped adding PRIMARY KEY [{constraint.Name}] due to data safety check";
            }

            return $"ALTER TABLE [{entity.Schema}].[{entity.Name}] ADD CONSTRAINT [{constraint.Name}] PRIMARY KEY ({cols});";
        }

        // فحص خاص بالـ UNIQUE (لو عايز تضيف أمان إضافى)
        if (constraint.Type.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            return $"ALTER TABLE [{entity.Schema}].[{entity.Name}] ADD CONSTRAINT [{constraint.Name}] UNIQUE ({cols});";
        }

        // TODO: دعم FOREIGN KEY
        if (constraint.Type.Equals("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
        {
            return $"-- TODO: Add FOREIGN KEY definition for [{constraint.Name}]";
        }

        return $"-- Unsupported constraint type: {constraint.Type} for [{constraint.Name}]";
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
        // 🗑️ فحص الفهارس المحذوفة
        foreach (var oldIdx in oldEntity.Indexes)
        {
            if (!newEntity.Indexes.Any(i => i.Name == oldIdx.Name))
            {
                sb.AppendLine($"-- ❌ Dropping index: {oldIdx.Name}");
                sb.AppendLine($"DROP INDEX [{oldIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];");
            }
        }

        // 🆕 فحص الفهارس الجديدة
        foreach (var newIdx in newEntity.Indexes)
        {
            if (!oldEntity.Indexes.Any(i => i.Name == newIdx.Name))
            {
                bool skipIndex = false;
                int totalBytes = 0;
                var colSizes = new List<string>();

                foreach (var colName in newIdx.Columns)
                {
                    var colDef = newEntity.Columns.FirstOrDefault(c =>
                        c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                    if (colDef != null)
                    {
                        int colBytes;

                        // ✅ لو النوع max، نفترض طول آمن 450 للحساب
                        if (colDef.TypeName.Contains("(max)", StringComparison.OrdinalIgnoreCase))
                        {
                            colBytes = GetColumnMaxLength("nvarchar(450)");
                            Console.WriteLine($"[INFO] Column {colDef.Name} is max — using safe length 450 for index size calculation.");
                        }
                        else
                        {
                            colBytes = GetColumnMaxLength(colDef.TypeName);
                        }

                        totalBytes += colBytes;
                        colSizes.Add($"{colDef.Name}={colBytes}B");
                    }
                }

                // ✅ فحص الحجم الكلي للفهرس المركّب
                if (totalBytes > 900)
                {
                    Console.WriteLine($"⚠️ Skipped creating index [{newIdx.Name}] on {newEntity.Schema}.{newEntity.Name} because total key size {totalBytes} bytes exceeds SQL Server index key limit (900 bytes). Columns: {string.Join(", ", colSizes)}");
                    sb.AppendLine($"-- Skipped creating index [{newIdx.Name}] due to total key size {totalBytes} bytes exceeding 900-byte index key limit");
                    skipIndex = true;
                }

                if (skipIndex)
                    continue;

                // ✅ إنشاء الفهرس لو كل الأعمدة مدعومة
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

    // 🛠️ ميثود مساعدة لحساب حجم العمود بالبايت
    private int GetColumnMaxLength(string typeName)
    {
        // مثال: nvarchar(150) → 150 * 2 بايت
        //       varchar(300)  → 300 * 1 بايت
        //       int           → 4 بايت
        //       decimal(18,2) → 9 بايت تقريبًا

        typeName = typeName.ToLowerInvariant();

        if (typeName.StartsWith("nvarchar"))
            return ExtractLengthForIndex(typeName) * 2;
        if (typeName.StartsWith("varchar"))
            return ExtractLengthForIndex(typeName);
        if (typeName.StartsWith("nchar"))
            return ExtractLengthForIndex(typeName) * 2;
        if (typeName.StartsWith("char"))
            return ExtractLengthForIndex(typeName);

        return typeName switch
        {
            "int" => 4,
            "bigint" => 8,
            "smallint" => 2,
            "tinyint" => 1,
            "bit" => 1,
            _ => 0
        };

    }

    // 🛠️ استخراج الطول من النص (مثال: nvarchar(150) → 150)
    private int ExtractLengthForIndex(string typeName)
    {
        var start = typeName.IndexOf('(');
        var end = typeName.IndexOf(')');
        if (start > 0 && end > start)
        {
            var numStr = typeName.Substring(start + 1, end - start - 1);
            if (int.TryParse(numStr, out int len))
                return len;
        }
        return 0;
    }

    /// <summary>
    /// Checks if an index (single or composite) exceeds SQL Server's 900-byte key size limit.
    /// </summary>
    private bool IsIndexTooLarge(IndexDefinition index, List<ColumnDefinition> allColumns)
    {
        int totalBytes = 0;

        foreach (var colName in index.Columns)
        {
            var col = allColumns.FirstOrDefault(c =>
                c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

            if (col != null)
            {
                totalBytes += GetColumnMaxLength(col.TypeName);

                // لو النوع max → نستخدم طول آمن مؤقت للحساب
                if (IsMaxType(col.TypeName))
                    totalBytes += GetColumnMaxLength("nvarchar(450)");
            }
        }

        return totalBytes > 900;
    }

    /// <summary>
    /// Detects if a SQL type is defined as (max).
    /// </summary>
    private bool IsMaxType(string typeName)
    {
        var start = typeName.IndexOf('(');
        var end = typeName.IndexOf(')');
        if (start > 0 && end > start)
        {
            var numStr = typeName.Substring(start + 1, end - start - 1).Trim();
            return numStr.Equals("max", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }





    private int ColumnNullCount(string schema, string tableName, string columnName)
    {
        var sql = $@"SELECT COUNT(*) FROM [{schema}].[{tableName}] WHERE [{columnName}] IS NULL";
        return ExecuteScalar<int>(sql);
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

    /// <summary>
    /// Determines if a column change can be applied using ALTER COLUMN instead of Drop & Add.
    /// </summary>
    private bool CanAlterColumn(ColumnDefinition oldCol, ColumnDefinition newCol, string schema, string tableName)
    {
        if (oldCol == null || newCol == null)
            return false;

        string baseOldType = oldCol.TypeName.Split('(')[0].Trim().ToLowerInvariant();
        string baseNewType = newCol.TypeName.Split('(')[0].Trim().ToLowerInvariant();

        if (!string.Equals(baseOldType, baseNewType, StringComparison.OrdinalIgnoreCase))
            return false;

        // ✅ تعديل الـ Identity فقط لو الجدول فاضي
        if (oldCol.IsIdentity != newCol.IsIdentity)
        {
            if (IsTableEmpty(schema, tableName))
            {
                Console.WriteLine($"[INFO] Table {schema}.{tableName} is empty → allowing Identity change for {newCol.Name}");
                return true;
            }
            else
            {
                Console.WriteLine($"⚠️ Skipped Identity change for {newCol.Name} because table has data.");
                return false;
            }
        }

        // ✅ التحويل إلى NOT NULL فقط لو مفيش NULL
        if (!newCol.IsNullable && oldCol.IsNullable)
        {
            if (ColumnHasNulls(schema, tableName, newCol.Name))
            {
                Console.WriteLine($"⚠️ Skipped NOT NULL change for {newCol.Name} because it contains NULL values.");
                return false;
            }
        }

        bool nullabilityChanged = oldCol.IsNullable != newCol.IsNullable;
        bool lengthChanged = false;

        if (baseOldType.Contains("char"))
        {
            int? oldLen = ExtractLength(oldCol.TypeName);
            int? newLen = ExtractLength(newCol.TypeName);
            if (oldLen != newLen)
                lengthChanged = true;
        }

        if (baseOldType.Contains("decimal") || baseOldType.Contains("numeric"))
        {
            if (oldCol.Precision != newCol.Precision || oldCol.Scale != newCol.Scale)
                lengthChanged = true;
        }

        if (lengthChanged || nullabilityChanged)
            return true;

        return false;
    }
    /// <summary>
    /// Extracts the length from a SQL type name like nvarchar(150).
    /// </summary>
    private int? ExtractLength(string typeName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(typeName, @"\((\d+)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int len))
            return len;
        return null;
    }

    /// <summary>
    /// Checks if a table has no rows.
    /// </summary>
    private bool IsTableEmpty(string schema, string tableName)
    {
        var sql = $@"
SELECT COUNT(*) 
FROM [{schema}].[{tableName}]";

        // ✅ استخدام ExecuteScalar<int> بدل الكود اليدوي
        int rowCount = ExecuteScalar<int>(sql);

        // ✅ تتبع واضح
        if (rowCount == 0)
            Console.WriteLine($"[TRACE:TableCheck] {schema}.{tableName} → Table is empty");
        else
            Console.WriteLine($"[TRACE:TableCheck] {schema}.{tableName} → Table has {rowCount} rows");

        return rowCount == 0;
    }


    /// <summary>
    /// Checks if a column contains any NULL values.
    /// </summary>
    private bool ColumnHasNulls(string schema, string tableName, string columnName)
    {
        var sql = $@"
SELECT COUNT(*) 
FROM [{schema}].[{tableName}] 
WHERE [{columnName}] IS NULL";

        // ✅ استخدام الميثود العامة ExecuteScalar<int>
        int count = ExecuteScalar<int>(sql);

        // ✅ تتبع واضح
        if (count > 0)
            Console.WriteLine($"[TRACE:NullCheck] {schema}.{tableName}.{columnName} → Found {count} NULL values");
        else
            Console.WriteLine($"[TRACE:NullCheck] {schema}.{tableName}.{columnName} → No NULL values found");

        return count > 0;
    }


    /// <summary>
    /// Executes a scalar SQL query and returns the result as T.
    /// </summary>
    private T ExecuteScalar<T>(string sql)
    {
        using (var conn = new SqlConnection(_connectionString))
        using (var cmd = new SqlCommand(sql, conn))
        {
            conn.Open();
            object result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return default(T);
            return (T)Convert.ChangeType(result, typeof(T));
        }
    }

    private bool CanAlterPrimaryKey(EntityDefinition oldEntity, EntityDefinition newEntity)
    {
        // لو مفيش PK في القديم أو الجديد → نعتبره مش اختلاف Identity-only
        if (oldEntity?.PrimaryKey?.Columns == null || newEntity?.PrimaryKey?.Columns == null)
            return true;

        // لو الأعمدة نفسها متطابقة في الاسم والترتيب
        bool sameCols = oldEntity.PrimaryKey?.Columns.SequenceEqual(newEntity.PrimaryKey.Columns, StringComparer.OrdinalIgnoreCase) ?? false;

        if (sameCols)
        {
            var pkColName = newEntity.PrimaryKey.Columns.First();
            var oldCol = oldEntity.Columns.FirstOrDefault(c => c.Name.Equals(pkColName, StringComparison.OrdinalIgnoreCase));
            var newCol = newEntity.Columns.FirstOrDefault(c => c.Name.Equals(pkColName, StringComparison.OrdinalIgnoreCase));

            // ✅ لو الاتنين نفس الـ Identity → مفيش داعي لأي تعديل
            if (oldCol != null && newCol != null && oldCol.IsIdentity == newCol.IsIdentity)
            {
                Console.WriteLine($"⚠️ Skipped PK change for {newEntity.Schema}.{newEntity.Name} because PK columns and Identity match.");
                return false;
            }

            // ✅ لو فيه اختلاف في الـ Identity لكن الجدول فيه بيانات → نتجنب التغيير
            if (oldCol != null && newCol != null && oldCol.IsIdentity != newCol.IsIdentity && !IsTableEmpty(newEntity.Schema, newEntity.Name))
            {
                Console.WriteLine($"⚠️ Skipped PK change for {newEntity.Schema}.{newEntity.Name} because table has data and change is Identity-only.");
                return false;
            }
        }

        return true;
    }



    #endregion
}