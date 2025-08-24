namespace Syn.Core.SqlSchemaGenerator.Migrations.Steps;

/// <summary>
/// Defines the type of operation performed in a migration step.
/// </summary>
public enum MigrationOperation
{
    AddColumn,
    DropColumn,
    AlterColumn,
    AddIndex,
    DropIndex,
    AddConstraint,
    DropConstraint,
    AddComputedColumn,
    DropComputedColumn,
    RenameColumn,
    CustomSql,
    AlterIndex,
    CreateEntity,
    DropEntity
}
