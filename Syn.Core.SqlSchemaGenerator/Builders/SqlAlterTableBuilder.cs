using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Builds SQL ALTER TABLE scripts by comparing old and new EntityDefinitions.
    /// This builder delegates all metadata construction to <see cref="EntityDefinitionBuilder"/>,
    /// ensuring a single source of truth for schema details (including identity detection).
    /// </summary>
    public class SqlAlterTableBuilder
    {
        private readonly EntityDefinitionBuilder _entityDefinitionBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlAlterTableBuilder"/> class.
        /// </summary>
        /// <param name="entityDefinitionBuilder">
        /// The EntityDefinitionBuilder instance that provides entity metadata for comparison.
        /// </param>
        public SqlAlterTableBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
        {
            _entityDefinitionBuilder = entityDefinitionBuilder
                ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        }

        /// <summary>
        /// Generates an ALTER TABLE SQL script by comparing two CLR type definitions.
        /// Uses <see cref="EntityDefinitionBuilder"/> to build both models before comparison.
        /// </summary>
        /// <param name="oldType">The old CLR type representing the existing table schema.</param>
        /// <param name="newType">The new CLR type representing the updated table schema.</param>
        /// <returns>SQL string with the ALTER TABLE commands to migrate from old to new schema.</returns>
        public string Build(Type oldType, Type newType)
        {
            if (oldType == null) throw new ArgumentNullException(nameof(oldType));
            if (newType == null) throw new ArgumentNullException(nameof(newType));

            var oldEntity = _entityDefinitionBuilder.Build(oldType);
            var newEntity = _entityDefinitionBuilder.Build(newType);

            return Build(oldEntity, newEntity);
        }

        /// <summary>
        /// Generates an ALTER TABLE SQL script by comparing two <see cref="EntityDefinition"/> instances.
        /// </summary>
        /// <param name="oldEntity">The entity definition for the existing table schema.</param>
        /// <param name="newEntity">The entity definition for the updated table schema.</param>
        /// <returns>SQL string with the ALTER TABLE commands to migrate from old to new schema.</returns>
        public string Build(EntityDefinition oldEntity, EntityDefinition newEntity)
        {
            if (oldEntity == null) throw new ArgumentNullException(nameof(oldEntity));
            if (newEntity == null) throw new ArgumentNullException(nameof(newEntity));

            var sb = new StringBuilder();

            // Example: Add new columns
            foreach (var col in newEntity.Columns)
            {
                if (!oldEntity.Columns.Exists(c => c.Name == col.Name))
                {
                    sb.AppendLine(
                        $"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD {BuildColumnDefinition(col)};"
                    );
                }
            }

            // Example: Drop removed columns
            foreach (var col in oldEntity.Columns)
            {
                if (!newEntity.Columns.Exists(c => c.Name == col.Name))
                {
                    sb.AppendLine(
                        $"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP COLUMN [{col.Name}];"
                    );
                }
            }

            // TODO: Extend with checks for modified columns, indexes, constraints, etc.

            return sb.ToString();
        }

        /// <summary>
        /// Builds the SQL definition for a single column based on its <see cref="ColumnDefinition"/>.
        /// </summary>
        /// <param name="col">The column definition.</param>
        /// <returns>SQL string for the column definition.</returns>
        private string BuildColumnDefinition(ColumnDefinition col)
        {
            var sb = new StringBuilder();

            sb.Append($"[{col.Name}] {col.TypeName}");

            if (!col.IsNullable)
                sb.Append(" NOT NULL");

            if (col.IsIdentity)
                sb.Append(" IDENTITY(1,1)");

            if (col.DefaultValue != null)
                sb.Append($" DEFAULT {FormatDefaultValue(col.DefaultValue)}");

            return sb.ToString();
        }

        /// <summary>
        /// Formats the default value for inclusion in SQL scripts.
        /// </summary>
        /// <param name="value">The default value object.</param>
        /// <returns>Formatted SQL literal.</returns>
        private string FormatDefaultValue(object value)
        {
            if (value is string s)
                return $"'{s}'";

            if (value is bool b)
                return b ? "1" : "0";

            return value.ToString();
        }
    }
}



//using Syn.Core.SqlSchemaGenerator.Core;
//using Syn.Core.SqlSchemaGenerator.Helper;
//using Syn.Core.SqlSchemaGenerator.Models;

//using System.Text;

//namespace Syn.Core.SqlSchemaGenerator.Builders
//{
//    /// <summary>
//    /// Placeholder for generating ALTER TABLE scripts between entity versions.
//    /// </summary>
//    public class SqlAlterTableBuilder
//    {
//        /// <summary>
//        /// Builds a SQL ALTER TABLE script to migrate from an old entity type to a new one.
//        /// Handles column additions/removals, type changes, nullability, default values, collation, computed expressions,
//        /// primary keys, unique constraints, indexes (single/composite), foreign keys, and warns about column order changes.
//        /// </summary>
//        /// <param name="oldType">The original entity type.</param>
//        /// <param name="newType">The updated entity type.</param>
//        /// <returns>SQL script to apply schema changes.</returns>
//        public string BuildAlterScript(Type oldType, Type newType)
//        {
//            var parser = new SqlEntityParser();
//            var oldCols = parser.Parse(oldType);
//            var newCols = parser.Parse(newType);
//            var oldFks = oldType.GetForeignKeys();
//            var newFks = newType.GetForeignKeys();
//            var oldIndexes = oldType.GetCompositeIndexes();
//            var newIndexes = newType.GetCompositeIndexes();

//            var (tableName, schema) = newType.ParseTableInfo();
//            var sb = new StringBuilder();

//            // 1. Add new columns
//            foreach (var col in newCols.Where(nc => !oldCols.Any(oc => oc.Name == nc.Name)))
//            {
//                var line = $"ALTER TABLE [{schema}].[{tableName}] ADD [{col.Name}] {col.TypeName}";
//                if (!col.IsNullable) line += " NOT NULL";
//                if (col.IsIdentity) line += " IDENTITY(1,1)";
//                if (!string.IsNullOrWhiteSpace(col.DefaultValue)) line += $" DEFAULT {col.DefaultValue}";
//                if (col.Collation != null) line += $" COLLATE {col.Collation}";
//                sb.AppendLine(line + ";");
//            }

//            // 2. Primary key change
//            var oldPk = oldCols.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
//            var newPk = newCols.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
//            if (!oldPk.SequenceEqual(newPk))
//            {
//                var pkName = $"PK_{tableName}";
//                sb.AppendLine($@"
//IF EXISTS (
//    SELECT 1 FROM sys.key_constraints 
//    WHERE [name] = '{pkName}' AND [parent_object_id] = OBJECT_ID('[{schema}].[{tableName}]')
//)
//ALTER TABLE [{schema}].[{tableName}] DROP CONSTRAINT [{pkName}];");

//                var pkCols = string.Join(", ", newPk.Select(c => $"[{c}]"));
//                sb.AppendLine($"ALTER TABLE [{schema}].[{tableName}] ADD CONSTRAINT [{pkName}] PRIMARY KEY ({pkCols});");
//            }

//            // 3. Nullability change
//            foreach (var col in newCols.Where(nc => oldCols.Any(oc => oc.Name == nc.Name && oc.IsNullable != nc.IsNullable)))
//            {
//                sb.AppendLine($"ALTER TABLE [{schema}].[{tableName}] ALTER COLUMN [{col.Name}] {col.TypeName} {(col.IsNullable ? "NULL" : "NOT NULL")};");
//            }

//            // 4. Default value change
//            foreach (var col in newCols.Where(nc => oldCols.Any(oc => oc.Name == nc.Name && oc.DefaultValue != nc.DefaultValue)))
//            {
//                var defName = $"DF_{tableName}_{col.Name}";
//                sb.AppendLine($@"
//IF EXISTS (
//    SELECT 1 FROM sys.default_constraints 
//    WHERE [name] = '{defName}' AND [parent_object_id] = OBJECT_ID('[{schema}].[{tableName}]')
//)
//ALTER TABLE [{schema}].[{tableName}] DROP CONSTRAINT [{defName}];");

//                if (!string.IsNullOrWhiteSpace(col.DefaultValue))
//                {
//                    sb.AppendLine($"ALTER TABLE [{schema}].[{tableName}] ADD CONSTRAINT [{defName}] DEFAULT {col.DefaultValue} FOR [{col.Name}];");
//                }
//            }

//            // 5. Collation change
//            foreach (var col in newCols.Where(nc => oldCols.Any(oc => oc.Name == nc.Name && oc.Collation != nc.Collation)))
//            {
//                sb.AppendLine($"ALTER TABLE [{schema}].[{tableName}] ALTER COLUMN [{col.Name}] {col.TypeName} COLLATE {col.Collation};");
//            }

//            // 6. TypeName / Precision / Scale change
//            foreach (var col in newCols.Where(nc => oldCols.Any(oc =>
//                oc.Name == nc.Name &&
//                (oc.TypeName != nc.TypeName || oc.Precision != nc.Precision || oc.Scale != nc.Scale))))
//            {
//                var line = $"ALTER TABLE [{schema}].[{tableName}] ALTER COLUMN [{col.Name}] {col.TypeName}";
//                line += col.IsNullable ? " NULL" : " NOT NULL";
//                if (col.Collation != null) line += $" COLLATE {col.Collation}";
//                sb.AppendLine(line + ";");
//            }

//            // 7. Computed expression change
//            foreach (var col in newCols.Where(nc => nc.IsComputed &&
//                oldCols.Any(oc => oc.Name == nc.Name && oc.ComputedExpression != nc.ComputedExpression)))
//            {
//                sb.AppendLine($"ALTER TABLE [{schema}].[{tableName}] DROP COLUMN [{col.Name}];");
//                sb.AppendLine($"ALTER TABLE [{schema}].[{tableName}] ADD [{col.Name}] AS ({col.ComputedExpression});");
//            }

//            // 8. Drop old unique constraints
//            foreach (var col in oldCols.Where(o => o.IsUnique && !newCols.Any(n => n.Name == o.Name && n.IsUnique)))
//            {
//                var uqName = $"UQ_{tableName}_{col.Name}";
//                sb.AppendLine($@"
//IF EXISTS (
//    SELECT 1 FROM sys.objects 
//    WHERE [name] = '{uqName}' AND [type] = 'UQ' AND [parent_object_id] = OBJECT_ID('[{schema}].[{tableName}]')
//)
//ALTER TABLE [{schema}].[{tableName}] DROP CONSTRAINT [{uqName}];");
//            }

//            // 9. Drop old single-column indexes
//            foreach (var col in oldCols.Where(o => o.HasIndex && !newCols.Any(n => n.Name == o.Name && n.HasIndex)))
//            {
//                var ixName = col.IndexName ?? $"IX_{tableName}_{col.Name}";
//                sb.AppendLine($@"
//IF EXISTS (
//    SELECT 1 FROM sys.indexes 
//    WHERE [name] = '{ixName}' AND [object_id] = OBJECT_ID('[{schema}].[{tableName}]')
//)
//DROP INDEX [{ixName}] ON [{schema}].[{tableName}];");
//            }

//            // 10. Drop old composite indexes
//            foreach (var ix in oldIndexes.Where(oi => !newIndexes.Any(ni =>
//                ni.Name == oi.Name &&
//                ni.IsUnique == oi.IsUnique &&
//                ni.Columns.SequenceEqual(oi.Columns))))
//            {
//                sb.AppendLine($@"
//IF EXISTS (
//    SELECT 1 FROM sys.indexes 
//    WHERE [name] = '{ix.Name}' AND [object_id] = OBJECT_ID('[{schema}].[{tableName}]')
//)
//DROP INDEX [{ix.Name}] ON [{schema}].[{tableName}];");
//            }

//            // 11. Add new unique constraints
//            foreach (var col in newCols.Where(c => c.IsUnique && !oldCols.Any(o => o.Name == c.Name && o.IsUnique)))
//            {
//                sb.AppendLine($"ALTER TABLE [{schema}].[{tableName}] ADD CONSTRAINT [UQ_{tableName}_{col.Name}] UNIQUE ([{col.Name}]);");
//            }

//            // 12. Add new single-column indexes
//            foreach (var col in newCols.Where(c => c.HasIndex && !oldCols.Any(o => o.Name == c.Name && o.HasIndex)))
//            {
//                var indexName = col.IndexName ?? $"IX_{tableName}_{col.Name}";
//                var unique = col.IsIndexUnique ? "UNIQUE " : "";
//                sb.AppendLine($"CREATE {unique}INDEX [{indexName}] ON [{schema}].[{tableName}] ([{col.Name}]);");
//            }

//            // 13. Add new composite indexes
//            foreach (var ix in newIndexes.Where(ni => !oldIndexes.Any(oi =>
//                oi.Name == ni.Name &&
//                oi.IsUnique == ni.IsUnique &&
//                oi.Columns.SequenceEqual(ni.Columns))))
//            {
//                var cols = string.Join(", ", ix.Columns.Select(c => $"[{c}]"));
//                var unique = ix.IsUnique ? "UNIQUE " : "";
//                sb.AppendLine($"CREATE {unique}INDEX [{ix.Name}] ON [{schema}].[{tableName}] ({cols});");
//            }
//            // 14. Add new foreign keys
//            foreach (var fk in newFks.Where(nfk => !oldFks.Any(ofk =>
//                ofk.Column == nfk.Column &&
//                ofk.ReferencedTable == nfk.ReferencedTable &&
//                ofk.ReferencedColumn == nfk.ReferencedColumn &&
//                ofk.OnDelete == nfk.OnDelete &&
//                ofk.OnUpdate == nfk.OnUpdate)))
//            {
//                var constraintName = $"FK_{tableName}_{fk.Column}";
//                var onDelete = fk.OnDelete switch
//                {
//                    ReferentialAction.Cascade => "ON DELETE CASCADE",
//                    ReferentialAction.SetNull => "ON DELETE SET NULL",
//                    ReferentialAction.SetDefault => "ON DELETE SET DEFAULT",
//                    _ => ""
//                };

//                var onUpdate = fk.OnUpdate switch
//                {
//                    ReferentialAction.Cascade => "ON UPDATE CASCADE",
//                    ReferentialAction.SetNull => "ON UPDATE SET NULL",
//                    ReferentialAction.SetDefault => "ON UPDATE SET DEFAULT",
//                    _ => ""
//                };

//                sb.AppendLine($@"ALTER TABLE [{schema}].[{tableName}] 
//ADD CONSTRAINT [{constraintName}] 
//FOREIGN KEY ([{fk.Column}]) 
//REFERENCES [{fk.ReferencedTable}]([{fk.ReferencedColumn}]) 
//{onDelete} {onUpdate};");
//            }

//            // 15. Column order change warning
//            var oldOrder = oldCols.Select(c => c.Name).ToList();
//            var newOrder = newCols.Select(c => c.Name).ToList();
//            if (!oldOrder.SequenceEqual(newOrder))
//            {
//                sb.AppendLine("-- ⚠️ Column order has changed. SQL Server does not support reordering columns via ALTER TABLE.");
//            }
//            return sb.Length > 0 ? sb.ToString() : "-- No schema changes detected";
//        }
//    }

//}