using Syn.Core.SqlSchemaGenerator.Helper;
using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Linq;
using System.Text;

using Syn.Core.SqlSchemaGenerator.Models;
using System;
using System.Linq;
using System.Text;

namespace Syn.Core.SqlSchemaGenerator.Builders
{
    /// <summary>
    /// Builds SQL CREATE TABLE scripts based on an EntityDefinition model.
    /// Delegates all metadata creation to <see cref="EntityDefinitionBuilder"/>,
    /// ensuring consistency and single source of truth for schema details.
    /// </summary>
    public class SqlTableScriptBuilder
    {
        private readonly EntityDefinitionBuilder _entityDefinitionBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTableScriptBuilder"/> class.
        /// </summary>
        /// <param name="entityDefinitionBuilder">The unified metadata builder for entities.</param>
        public SqlTableScriptBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
        {
            _entityDefinitionBuilder = entityDefinitionBuilder
                ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
        }

        /// <summary>
        /// Generates a CREATE TABLE SQL script from a CLR type.
        /// </summary>
        public string Build(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            var entityDefinition = _entityDefinitionBuilder.Build(entityType);
            return Build(entityDefinition);
        }

        /// <summary>
        /// Generates a CREATE TABLE SQL script from an EntityDefinition.
        /// </summary>
        public string Build(EntityDefinition entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var schema = string.IsNullOrWhiteSpace(entity.Schema) ? "dbo" : entity.Schema;
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [{schema}].[{entity.Name}] (");

            var columnLines = entity.Columns.Select(BuildColumnDefinition).ToList();

            if (entity.PrimaryKey != null && entity.PrimaryKey.Columns.Any())
            {
                var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
                columnLines.Add($"CONSTRAINT [{entity.PrimaryKey.Name}] PRIMARY KEY ({pkCols})");
            }

            sb.AppendLine("    " + string.Join(",\n    ", columnLines));
            sb.AppendLine(");");

            return sb.ToString();
        }

        private string BuildColumnDefinition(ColumnDefinition col)
        {
            var sb = new StringBuilder();
            sb.Append($"[{col.Name}] {col.TypeName}");
            if (!col.IsNullable) sb.Append(" NOT NULL");
            if (col.IsIdentity) sb.Append(" IDENTITY(1,1)");
            if (col.DefaultValue != null) sb.Append($" DEFAULT {FormatDefaultValue(col.DefaultValue)}");
            return sb.ToString();
        }

        private string FormatDefaultValue(object value) =>
            value switch
            {
                string s => $"'{s}'",
                bool b => b ? "1" : "0",
                _ => value.ToString()
            };
    }
}



//using Syn.Core.SqlSchemaGenerator.Helper;
//using Syn.Core.SqlSchemaGenerator.Models;

//    using System;
//    using System.Linq;
//    using System.Text;

//    namespace Syn.Core.SqlSchemaGenerator.Builders
//    {
//        /// <summary>
//        /// Builds SQL CREATE TABLE scripts based on an EntityDefinition model.
//        /// This builder delegates all metadata construction to <see cref="EntityDefinitionBuilder"/>,
//        /// ensuring a single source of truth for schema details (including identity detection).
//        /// </summary>
//        public class SqlTableScriptBuilder
//        {
//            private readonly EntityDefinitionBuilder _entityDefinitionBuilder;

//            /// <summary>
//            /// Initializes a new instance of the <see cref="SqlTableScriptBuilder"/> class.
//            /// </summary>
//            /// <param name="entityDefinitionBuilder">
//            /// The EntityDefinitionBuilder instance that provides the entity metadata.
//            /// </param>
//            public SqlTableScriptBuilder(EntityDefinitionBuilder entityDefinitionBuilder)
//            {
//                _entityDefinitionBuilder = entityDefinitionBuilder
//                    ?? throw new ArgumentNullException(nameof(entityDefinitionBuilder));
//            }

//            /// <summary>
//            /// Generates a CREATE TABLE SQL script from a CLR type definition.
//            /// Uses <see cref="EntityDefinitionBuilder"/> to build the entity model.
//            /// </summary>
//            /// <param name="entityType">The CLR type to generate a table script for.</param>
//            /// <returns>SQL string for creating the table represented by the type.</returns>
//            public string Build(Type entityType)
//            {
//                if (entityType == null) throw new ArgumentNullException(nameof(entityType));

//                var entityDefinition = _entityDefinitionBuilder.Build(entityType);
//                return Build(entityDefinition);
//            }

//            /// <summary>
//            /// Generates a CREATE TABLE SQL script from a pre-built <see cref="EntityDefinition"/>.
//            /// </summary>
//            /// <param name="entity">The entity definition to generate a table script for.</param>
//            /// <returns>SQL string for creating the table.</returns>
//            public string Build(EntityDefinition entity)
//            {
//                if (entity == null) throw new ArgumentNullException(nameof(entity));

//                var sb = new StringBuilder();

//                sb.AppendLine($"CREATE TABLE [{entity.Schema}].[{entity.Name}] (");

//                // Build column definitions
//                var columnLines = entity.Columns
//                    .Select(col => BuildColumnDefinition(col))
//                    .ToList();

//                // Append primary key if defined
//                if (entity.PrimaryKey != null && entity.PrimaryKey.Columns.Any())
//                {
//                    var pkCols = string.Join(", ", entity.PrimaryKey.Columns.Select(c => $"[{c}]"));
//                    columnLines.Add($"CONSTRAINT [{entity.PrimaryKey.Name}] PRIMARY KEY ({pkCols})");
//                }

//                // Join all lines with commas
//                sb.AppendLine("    " + string.Join(",\n    ", columnLines));

//                sb.AppendLine(");");

//                return sb.ToString();
//            }

//            /// <summary>
//            /// Builds the SQL definition for a single column based on its <see cref="ColumnDefinition"/>.
//            /// </summary>
//            /// <param name="col">The column definition.</param>
//            /// <returns>SQL string for the column.</returns>
//            private string BuildColumnDefinition(ColumnDefinition col)
//            {
//                var sb = new StringBuilder();

//                sb.Append($"[{col.Name}] {col.TypeName}");

//                if (!col.IsNullable)
//                    sb.Append(" NOT NULL");

//                if (col.IsIdentity)
//                    sb.Append(" IDENTITY(1,1)");

//                if (col.DefaultValue != null)
//                    sb.Append($" DEFAULT {FormatDefaultValue(col.DefaultValue)}");

//                return sb.ToString();
//            }

//            /// <summary>
//            /// Formats the default value for inclusion in SQL scripts.
//            /// </summary>
//            /// <param name="value">The default value object.</param>
//            /// <returns>Formatted SQL literal.</returns>
//            private string FormatDefaultValue(object value)
//            {
//                if (value is string s)
//                    return $"'{s}'";

//                if (value is bool b)
//                    return b ? "1" : "0";

//                return value.ToString();
//            }
//        }
//    }
