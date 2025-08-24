# تقرير مسح SqlSchemaGenerator

## Project: Syn.Core.SqlSchemaGenerator

### class `CheckConstraintAttributeHandler`
- **Methods:**
  - Apply()

### class `CollationAttributeHandler`
- **Methods:**
  - Apply()

### class `ComputedAttributeHandler`
- **Methods:**
  - Apply()

### class `DefaultValueAttributeHandler`
- **Methods:**
  - Apply()

### class `DescriptionAttributeHandler`
- **Methods:**
  - Apply()

### class `EfCompatibilityAttributeHandler`
- **Methods:**
  - Apply()

### class `IgnoreColumnAttributeHandler`
- **Methods:**
  - Apply()

### class `IndexAttributeHandler`
- **Methods:**
  - Apply()

### class `MaxLengthAttributeHandler`
- **Methods:**
  - Apply()

### class `RequiredAttributeHandler`
- **Methods:**
  - Apply()

### class `UniqueAttributeHandler`
- **Methods:**
  - Apply()

### class `CheckConstraintAttribute`
- **Properties:**
  - `string` Expression
  - `string?` Name

### class `CollationAttribute`
- **Properties:**
  - `string` Collation

### class `ComputedAttribute`
- **Properties:**
  - `string` SqlExpression
  - `bool` IsPersisted

### class `DefaultValueAttribute`
- **Properties:**
  - `object?` Value

### class `DescriptionAttribute`
- **Properties:**
  - `string` Text

### class `IgnoreColumnAttribute`

### class `IndexAttribute`
- **Properties:**
  - `string?` Name
  - `bool` IsUnique
  - `string[]?` IncludeColumns

### class `UniqueAttribute`
- **Properties:**
  - `string?` ConstraintName

### class `EntityDefinitionBuilder`
- **Methods:**
  - Build()
  - ToColumnDefinition()
  - ToComputed()
  - ToCheckConstraints()
  - ToIndexes()
  - InferSqlType()

### class `SqlAlterTableBuilder`
- **Methods:**
  - BuildAlterScript()

### class `SqlDropTableBuilder`
- **Methods:**
  - Build()

### class `SqlTableBuilder`
- **Methods:**
  - Build()

### class `DbEntityScanner`
- **Methods:**
  - Scan()

### class `SqlEntityParser`
- **Methods:**
  - Parse()

### class `SqlTypeMapper`
- **Methods:**
  - Map()

### class `SqlValueFormatter`
- **Methods:**
  - Format()

### class `SqlSchemaDeployer`
- **Methods:**
  - DeployCreateScripts()
  - DeployDropScripts()

### class `SqlScriptRunner`
- **Properties:**
  - `int` CommandTimeout
- **Methods:**
  - ExecuteScript()
  - SplitScriptByGo()

### class `SqlSchemaExecutor`
- **Methods:**
  - GenerateCreateScripts()
  - GenerateDropScripts()
  - GenerateCreateScript()
  - GenerateDropScript()

### class `ComputedColumnConverter`
- **Methods:**
  - ToColumnModel()
  - ToComputedColumnDefinition()

### class `ConstraintConverter`
- **Methods:**
  - FromCheckDefinition()
  - ExtractColumnsFromExpression()

### class `EntityDefinitionConverter`
- **Methods:**
  - ToEntityModel()

### class `SchemaSnapshot`
- **Properties:**
  - `string` EntityName
  - `string` Version
  - `List<ColumnDefinition>` Columns

### class `SqlServerSchemaDiffer`
- **Methods:**
  - GenerateAlterTableScripts()

### class `MigrationEngine`
- **Methods:**
  - GenerateMigrations()
  - GenerateScript()

### class `MigrationResult`
- **Properties:**
  - `List<string>` ExecutedScripts
  - `List<string>` SkippedScripts

### class `MigrationStepBuilder`
- **Methods:**
  - BuildSteps()
  - CompareColumns()
  - CompareComputedColumns()
  - CompareIndexes()
  - CompareCheckConstraints()
  - ExtractIndexes()
  - ColumnChanged()
  - IndexChanged()

### class `SchemaDiffer`
- **Methods:**
  - Diff()
  - AreColumnsEqual()

### class `SchemaMigrationService`
- **Methods:**
  - ApplyMigrationsAsync()
  - HasBeenExecutedAsync()
  - HasBeenExecutedAsync()
  - RecordMigrationAsync()
  - EnsureSchemaHistoryTableExistsAsync()
  - RecordMigrationAsync()

### class `SqlMigrationScript`
- **Properties:**
  - `string` EntityName
  - `string` Version
  - `string` Sql
  - `string` Hash
- **Methods:**
  - ComputeHash()

### class `MigrationStep`
- **Properties:**
  - `MigrationOperation` Operation
  - `string` EntityName
  - `string` Schema
  - `string?` ColumnName
  - `string?` Sql
  - `string?` Description
  - `Dictionary<string, object>?` Metadata

### class `CheckConstraintDefinition`
- **Properties:**
  - `string` Name
  - `string` Expression
  - `string?` Description

### class `CheckConstraintModel`
- **Properties:**
  - `string` Expression
  - `string?` Name

### class `ColumnDefinition`
- **Properties:**
  - `string` Name
  - `string` TypeName
  - `bool` IsNullable
  - `object?` DefaultValue
  - `string?` Collation
  - `string?` Description
  - `bool` IsUnique
  - `string?` UniqueConstraintName
  - `List<IndexDefinition>` Indexes
  - `List<CheckConstraintDefinition>` CheckConstraints
  - `bool` IsIgnored
  - `string?` IgnoreReason
  - `bool` IsPrimaryKey
  - `bool` IsForeignKey
  - `string?` ForeignKeyTarget
  - `int?` Order

### class `ColumnModel`
- **Properties:**
  - `string` Name
  - `Type` PropertyType
  - `bool` IsIgnored
  - `string?` IgnoreReason
  - `bool` IsComputed
  - `string?` ComputedExpression
  - `string?` ComputedSource
  - `bool` IsPersisted
  - `object?` DefaultValue
  - `string?` Collation
  - `List<CheckConstraintModel>` CheckConstraints
  - `List<IndexModel>` Indexes
  - `bool` IsUnique
  - `string?` UniqueConstraintName
  - `string?` Description
  - `bool` IsNullable
  - `string?` TypeName
  - `string?` SourceEntity
  - `bool` IsPrimaryKey
  - `bool` IsForeignKey
  - `string?` ForeignKeyTarget
  - `int?` Order
  - `int` MaxLength

### class `ComputedColumnDefinition`
- **Properties:**
  - `string` Name
  - `string` DataType
  - `string` Expression
  - `bool` IsPersisted
  - `string?` Description
  - `string?` Source
  - `bool` IsIgnored
  - `string?` IgnoreReason
  - `int?` Order

### class `ConstraintModel`
- **Properties:**
  - `string` Name
  - `ConstraintType` Type
  - `string` Expression
  - `string` Description
  - `List<string>` Columns
  - `string?` ForeignKeyTargetTable
  - `List<string>?` ForeignKeyTargetColumns
  - `bool` IsEnforced
  - `bool` IsTrusted

### class `EntityDefinition`
- **Properties:**
  - `string` Name
  - `string` Schema
  - `List<ColumnDefinition>` Columns
  - `List<IndexDefinition>` Indexes
  - `List<CheckConstraintDefinition>` CheckConstraints
  - `List<ComputedColumnDefinition>` ComputedColumns
  - `string?` Description
  - `bool` IsIgnored

### class `EntityModel`
- **Properties:**
  - `string` Name
  - `string` Schema
  - `string` Version
  - `string?` Description
  - `List<ColumnModel>` Columns
  - `List<string>` Constraints
  - `List<IndexModel>` TableIndexes
  - `List<ColumnModel>` ComputedColumns
  - `bool` IsIgnored
  - `string?` Source
  - `List<string>` Tags
  - `bool` IsView

### class `IndexDefinition`
- **Properties:**
  - `string` Name
  - `List<string>` Columns
  - `bool` IsUnique
  - `string?` FilterExpression
  - `string?` Description

### class `IndexModel`
- **Properties:**
  - `string?` Name
  - `bool` IsUnique
  - `List<string>` Columns

### class `SqlColumnInfo`
- **Properties:**
  - `string` Name
  - `Type` Type
  - `bool` IsPrimaryKey
  - `bool` IsUnique
  - `bool` IsComputed
  - `string` ComputedExpression
  - `string` ForeignKeyTargetTable
  - `string` ForeignKeyTargetColumn

### class `EntityScanner`
- **Methods:**
  - Scan()
  - IsValidEntity()

### class `SchemaBuilder`
- **Methods:**
  - Build()

### class `InMemorySnapshotProvider`
- **Methods:**
  - LoadSnapshot()

### class `SqlMigrationGenerator`
- **Methods:**
  - GenerateSql()
  - GenerateCreateEntity()
  - GenerateDropEntity()
  - GenerateAddColumn()
  - GenerateDropColumn()
  - GenerateAlterColumn()
  - GenerateAddIndex()
  - GenerateDropIndex()
  - GenerateAlterIndex()
  - GenerateAddConstraint()
  - GenerateDropConstraint()
  - GenerateForeignKeyConstraint()
  - BuildColumnDefinition()
  - BuildType()
  - FormatDefaultValue()

### class `SqlStatementGenerator`
- **Methods:**
  - GenerateAddColumn()
  - GenerateDropColumn()
  - GenerateAlterColumn()
  - MapType()
  - NullableSuffix()

### class `SqlSchemaSnapshotStore`
- **Methods:**
  - SaveSnapshot()
  - GetSnapshot()
  - GetLatestSnapshot()
  - ListVersions()
  - EnsureSnapshotTableExists()

## Project: Syn.CodeScanner

### class `Program`
- **Methods:**
  - Main()

