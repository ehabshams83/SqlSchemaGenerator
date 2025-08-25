using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Linq;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Generates ALTER TABLE SQL scripts to migrate an existing table definition
    /// to match a target definition. Supports columns, indexes, PK/FK/Unique constraints,
    /// and Check Constraints.
    /// </summary>
    public class SqlAlterTableBuilder
    {
        private readonly EntityDefinitionBuilder _entityDefinitionBuilder;

        /// <summary>
        /// Initializes a new instance using an <see cref="EntityDefinitionBuilder"/> for schema extraction.
        /// </summary>
        public SqlAlterTableBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
        {
            _entityDefinitionBuilder = entityDefinitionBuilder
                ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        }

        /// <summary>
        /// Builds ALTER TABLE SQL script comparing two <see cref="EntityDefinition"/> objects.
        /// </summary>
        public string Build(EntityDefinition oldEntity, EntityDefinition newEntity)
        {
            if (oldEntity == null) throw new ArgumentNullException(nameof(oldEntity));
            if (newEntity == null) throw new ArgumentNullException(nameof(newEntity));

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
            foreach (var col in newEntity.Columns)
            {
                if (!oldEntity.Columns.Any(c => c.Name == col.Name))
                    sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] ADD {BuildColumnDefinition(col)};");
            }

            foreach (var col in oldEntity.Columns)
            {
                if (!newEntity.Columns.Any(c => c.Name == col.Name))
                    sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP COLUMN [{col.Name}];");
            }
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
                if (!newEntity.Constraints.Any(c => c.Name == oldConst.Name))
                    sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldConst.Name}];");
            }

            foreach (var newConst in newEntity.Constraints)
            {
                if (!oldEntity.Constraints.Any(c => c.Name == newConst.Name))
                    sb.AppendLine(BuildAddConstraintSql(newEntity, newConst));
            }
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

                if (match == null || !string.Equals(
                        Normalize(match.Expression),
                        Normalize(oldCheck.Expression),
                        StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine(
                        $"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldCheck.Name}];"
                    );
                }
            }

            foreach (var newCheck in newEntity.CheckConstraints)
            {
                var match = oldEntity.CheckConstraints.FirstOrDefault(c => c.Name == newCheck.Name);

                if (match == null || !string.Equals(
                        Normalize(match.Expression),
                        Normalize(newCheck.Expression),
                        StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine(
                        $"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] " +
                        $"ADD CONSTRAINT [{newCheck.Name}] CHECK ({newCheck.Expression});"
                    );
                }
            }
        }

        private string Normalize(string input) =>
            input?.Trim().Replace("(", "").Replace(")", "").Replace(" ", "") ?? string.Empty;
        #endregion

        #region === Indexes ===
        private void AppendIndexChanges(StringBuilder sb, EntityDefinition oldEntity, EntityDefinition newEntity)
        {
            // Drop removed indexes
            foreach (var oldIdx in oldEntity.Indexes)
            {
                if (!newEntity.Indexes.Any(i => i.Name == oldIdx.Name))
                    sb.AppendLine($"DROP INDEX [{oldIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}];");
            }

            // Add new indexes
            foreach (var newIdx in newEntity.Indexes)
            {
                if (!oldEntity.Indexes.Any(i => i.Name == newIdx.Name))
                {
                    var cols = string.Join(", ", newIdx.Columns.Select(c => $"[{c}]"));
                    var unique = newIdx.IsUnique ? "UNIQUE " : "";
                    sb.AppendLine(
                        $"CREATE {unique}INDEX [{newIdx.Name}] ON [{newEntity.Schema}].[{newEntity.Name}] ({cols});"
                    );
                }
            }
        }
        #endregion

        #region === Foreign Keys ===
        private void AppendForeignKeyChanges(StringBuilder sb, EntityDefinition oldEntity, EntityDefinition newEntity)
        {
            var oldFks = oldEntity.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();
            var newFks = newEntity.Constraints.Where(c => c.Type == "FOREIGN KEY").ToList();

            // Drop removed FKs
            foreach (var oldFk in oldFks)
            {
                if (!newFks.Any(f => f.Name == oldFk.Name))
                    sb.AppendLine($"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] DROP CONSTRAINT [{oldFk.Name}];");
            }

            // Add new/changed FKs
            foreach (var newFk in newFks)
            {
                var match = oldFks.FirstOrDefault(f => f.Name == newFk.Name);

                var changed = match == null ||
                              match.ReferencedTable != newFk.ReferencedTable ||
                              !match.Columns.SequenceEqual(newFk.Columns) ||
                              !match.ReferencedColumns.SequenceEqual(newFk.ReferencedColumns);

                if (changed)
                {
                    var cols = string.Join(", ", newFk.Columns.Select(c => $"[{c}]"));
                    var refCols = string.Join(", ", newFk.ReferencedColumns.Select(c => $"[{c}]"));

                    sb.AppendLine(
                        $"ALTER TABLE [{newEntity.Schema}].[{newEntity.Name}] " +
                        $"ADD CONSTRAINT [{newFk.Name}] FOREIGN KEY ({cols}) " +
                        $"REFERENCES {newFk.ReferencedTable} ({refCols});"
                    );
                }
            }
        }
        #endregion
    }
}