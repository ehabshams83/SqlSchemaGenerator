using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Syn.Core.SqlSchemaGenerator
{
    /// <summary>
    /// Reads SQL Server database schema into <see cref="EntityDefinition"/> objects.
    /// Uses a unified read pipeline and splits output into separate collections.
    /// </summary>
    public class DatabaseSchemaReader
    {
        private readonly DbConnection _connection;

        public DatabaseSchemaReader(DbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public EntityDefinition GetEntityDefinition(string schemaName, string tableName)
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            var entity = new EntityDefinition
            {
                Schema = schemaName,
                Name = tableName,
                Columns = new List<ColumnDefinition>(),
                Indexes = new List<IndexDefinition>(),
                Constraints = new List<ConstraintDefinition>(),      // PK/FK/Unique
                CheckConstraints = new List<CheckConstraintDefinition>() // CHECK
            };

            ReadColumns(entity);
            if (!entity.Columns.Any())
                return null;

            ReadIndexes(entity);
            ReadConstraintsAndChecks(entity);

            return entity;
        }

        public List<EntityDefinition> GetAllEntities()
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            var results = new List<EntityDefinition>();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT TABLE_SCHEMA, TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var def = GetEntityDefinition(reader.GetString(0), reader.GetString(1));
                if (def != null) results.Add(def);
            }

            return results;
        }

        // ===== Private unified readers =====

        private void ReadColumns(EntityDefinition entity)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
        SELECT 
            c.COLUMN_NAME, 
            c.DATA_TYPE, 
            c.IS_NULLABLE, 
            c.COLUMN_DEFAULT,
            col.is_identity
        FROM INFORMATION_SCHEMA.COLUMNS c
        INNER JOIN sys.columns col 
            ON col.name = c.COLUMN_NAME
        INNER JOIN sys.objects o 
            ON o.object_id = col.object_id
        INNER JOIN sys.schemas s 
            ON s.schema_id = o.schema_id
        WHERE c.TABLE_SCHEMA = @schema 
          AND c.TABLE_NAME = @table
          AND o.name = @table
          AND s.name = @schema
        ORDER BY c.ORDINAL_POSITION";

            AddParam(cmd, "@schema", entity.Schema);
            AddParam(cmd, "@table", entity.Name);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                entity.Columns.Add(new ColumnDefinition
                {
                    Name = reader.GetString(0),
                    TypeName = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                    DefaultValue = reader.IsDBNull(3) ? null : reader.GetValue(3),
                    IsIdentity = !reader.IsDBNull(4) && reader.GetBoolean(4)
                });
            }
        }
        private void ReadIndexes(EntityDefinition entity)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT i.name AS IndexName,
                       i.is_unique,
                       c.name AS ColumnName
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic 
                    ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c 
                    ON ic.object_id = c.object_id AND c.column_id = ic.column_id
                INNER JOIN sys.objects o 
                    ON i.object_id = o.object_id
                INNER JOIN sys.schemas s 
                    ON o.schema_id = s.schema_id
                WHERE o.name = @table AND s.name = @schema
                      AND i.is_primary_key = 0
                ORDER BY i.name, ic.key_ordinal";

            AddParam(cmd, "@schema", entity.Schema);
            AddParam(cmd, "@table", entity.Name);

            using var reader = cmd.ExecuteReader();
            var indexGroups = new Dictionary<string, IndexDefinition>();

            while (reader.Read())
            {
                var idxName = reader.GetString(0);
                if (!indexGroups.TryGetValue(idxName, out var idxDef))
                {
                    idxDef = new IndexDefinition
                    {
                        Name = idxName,
                        IsUnique = reader.GetBoolean(1),
                        Columns = new List<string>()
                    };
                    indexGroups[idxName] = idxDef;
                    entity.Indexes.Add(idxDef);
                }
                idxDef.Columns.Add(reader.GetString(2));
            }
        }

        /// <summary>
        /// Reads all relational and check constraints for the given table
        /// and populates the corresponding collections in the <see cref="EntityDefinition"/>.
        /// 
        /// Populates:
        /// - <see cref="EntityDefinition.Constraints"/> with:
        ///   * PRIMARY KEY constraints
        ///   * UNIQUE constraints
        ///   * FOREIGN KEY constraints (including referenced table and columns)
        /// - <see cref="EntityDefinition.CheckConstraints"/> with:
        ///   * CHECK constraints and their expressions
        /// </summary>
        /// <param name="entity">
        /// The <see cref="EntityDefinition"/> to populate.
        /// Must have <see cref="EntityDefinition.Schema"/> and <see cref="EntityDefinition.Name"/> set.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="entity"/> is null.
        /// </exception>
        internal void ReadConstraintsAndChecks(EntityDefinition entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // ===== 1) Read PK / UNIQUE constraints =====
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
            SELECT 
                tc.CONSTRAINT_NAME, 
                tc.CONSTRAINT_TYPE, 
                kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
            WHERE tc.TABLE_SCHEMA = @schema 
              AND tc.TABLE_NAME = @table
              AND tc.CONSTRAINT_TYPE IN ('PRIMARY KEY', 'UNIQUE')
            ORDER BY tc.CONSTRAINT_NAME, kcu.ORDINAL_POSITION";

                AddParam(cmd, "@schema", entity.Schema);
                AddParam(cmd, "@table", entity.Name);

                using var reader = cmd.ExecuteReader();
                var constraintGroups = new Dictionary<string, ConstraintDefinition>();

                while (reader.Read())
                {
                    var constName = reader.GetString(0);
                    if (!constraintGroups.TryGetValue(constName, out var constDef))
                    {
                        constDef = new ConstraintDefinition
                        {
                            Name = constName,
                            Type = reader.GetString(1),
                            Columns = new List<string>()
                        };
                        constraintGroups[constName] = constDef;
                        entity.Constraints.Add(constDef);
                    }
                    constraintGroups[constName].Columns.Add(reader.GetString(2));
                }
            }

            // ===== 2) Read FOREIGN KEY constraints with reference details =====
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
            SELECT 
                rc.CONSTRAINT_NAME,
                kcu.COLUMN_NAME,
                kcu2.TABLE_SCHEMA AS RefSchema,
                kcu2.TABLE_NAME   AS RefTable,
                kcu2.COLUMN_NAME  AS RefColumn
            FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu2
                ON rc.UNIQUE_CONSTRAINT_NAME = kcu2.CONSTRAINT_NAME
            WHERE kcu.TABLE_SCHEMA = @schema
              AND kcu.TABLE_NAME = @table
            ORDER BY kcu.ORDINAL_POSITION";

                AddParam(cmd, "@schema", entity.Schema);
                AddParam(cmd, "@table", entity.Name);

                using var reader = cmd.ExecuteReader();
                var fkGroups = new Dictionary<string, ConstraintDefinition>();

                while (reader.Read())
                {
                    var fkName = reader.GetString(0);
                    if (!fkGroups.TryGetValue(fkName, out var fkDef))
                    {
                        fkDef = new ConstraintDefinition
                        {
                            Name = fkName,
                            Type = "FOREIGN KEY",
                            Columns = new List<string>(),
                            ReferencedTable = $"[{reader.GetString(2)}].[{reader.GetString(3)}]",
                            ReferencedColumns = new List<string>()
                        };
                        fkGroups[fkName] = fkDef;
                        entity.Constraints.Add(fkDef);
                    }

                    fkGroups[fkName].Columns.Add(reader.GetString(1));
                    fkGroups[fkName].ReferencedColumns.Add(reader.GetString(4));
                }
            }

            // ===== 3) Read CHECK constraints =====
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
            SELECT 
                cc.CONSTRAINT_NAME, 
                cc.CHECK_CLAUSE
            FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
            INNER JOIN INFORMATION_SCHEMA.CONSTRAINT_TABLE_USAGE tcu
                ON cc.CONSTRAINT_NAME = tcu.CONSTRAINT_NAME
            WHERE tcu.TABLE_SCHEMA = @schema 
              AND tcu.TABLE_NAME = @table";

                AddParam(cmd, "@schema", entity.Schema);
                AddParam(cmd, "@table", entity.Name);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    entity.CheckConstraints.Add(new CheckConstraintDefinition
                    {
                        Name = reader.GetString(0),
                        Expression = reader.GetString(1)
                    });
                }
            }
        }
        private void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
    }
}