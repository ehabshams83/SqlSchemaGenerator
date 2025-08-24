using Syn.Core.SqlSchemaGenerator.Migrations.Steps;
using Syn.Core.SqlSchemaGenerator.Models;
using Syn.Core.SqlSchemaGenerator.Converters;

namespace Syn.Core.SqlSchemaGenerator.Migrations;

/// <summary>
/// Builds a list of migration steps by comparing two versions of an entity definition.
/// Supports columns, computed columns, indexes, and check constraints.
/// </summary>
public class MigrationStepBuilder : IMigrationStepBuilder
{
    /// <summary>
    /// Generates migration steps between two schema snapshots.
    /// </summary>
    public List<MigrationStep> BuildSteps(List<EntityDefinition> oldSchema, List<EntityDefinition> newSchema)
    {
        var steps = new List<MigrationStep>();

        var oldMap = oldSchema.ToDictionary(e => $"{e.Schema}.{e.Name}");
        var newMap = newSchema.ToDictionary(e => $"{e.Schema}.{e.Name}");

        foreach (var newEntity in newMap.Values)
        {
            if (!oldMap.TryGetValue($"{newEntity.Schema}.{newEntity.Name}", out var oldEntity))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.CreateEntity,
                    EntityName = newEntity.Name,
                    Schema = newEntity.Schema,
                    Description = $"Create entity '{newEntity.Name}'",
                    Metadata = new Dictionary<string, object> { ["Entity"] = newEntity.ToEntityModel() }
                });
            }
            else
            {
                var oldModel = oldEntity.ToEntityModel();
                var newModel = newEntity.ToEntityModel();

                steps.AddRange(CompareColumns(oldModel, newModel));
                steps.AddRange(CompareComputedColumns(oldModel, newModel));
                steps.AddRange(CompareIndexes(oldModel, newModel));
                steps.AddRange(CompareCheckConstraints(oldModel, newModel));
            }
        }

        foreach (var oldEntity in oldMap.Values)
        {
            if (!newMap.ContainsKey($"{oldEntity.Schema}.{oldEntity.Name}"))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.DropEntity,
                    EntityName = oldEntity.Name,
                    Schema = oldEntity.Schema,
                    Description = $"Drop entity '{oldEntity.Name}'",
                    Metadata = new Dictionary<string, object> { ["Entity"] = oldEntity.ToEntityModel() }
                });
            }
        }

        return steps;
    }

    /// <summary>
    /// Compares regular columns between two entity models.
    /// </summary>
    private List<MigrationStep> CompareColumns(EntityModel oldEntity, EntityModel newEntity)
    {
        var steps = new List<MigrationStep>();

        var oldColumns = oldEntity.Columns.Where(c => !c.IsComputed).ToDictionary(c => c.Name);
        var newColumns = newEntity.Columns.Where(c => !c.IsComputed).ToDictionary(c => c.Name);

        foreach (var newCol in newColumns.Values)
        {
            if (!oldColumns.TryGetValue(newCol.Name, out var oldCol))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.AddColumn,
                    EntityName = newEntity.Name,
                    Schema = newEntity.Schema,
                    Description = $"Add column '{newCol.Name}'",
                    Metadata = new Dictionary<string, object> { ["Column"] = newCol }
                });
            }
            else if (ColumnChanged(oldCol, newCol))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.AlterColumn,
                    EntityName = newEntity.Name,
                    Schema = newEntity.Schema,
                    Description = $"Alter column '{newCol.Name}'",
                    Metadata = new Dictionary<string, object>
                    {
                        ["OldColumn"] = oldCol,
                        ["NewColumn"] = newCol
                    }
                });
            }
        }

        foreach (var oldCol in oldColumns.Values)
        {
            if (!newColumns.ContainsKey(oldCol.Name))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.DropColumn,
                    EntityName = oldEntity.Name,
                    Schema = oldEntity.Schema,
                    Description = $"Drop column '{oldCol.Name}'",
                    Metadata = new Dictionary<string, object> { ["Column"] = oldCol }
                });
            }
        }

        return steps;
    }

    /// <summary>
    /// Compares computed columns between two entity models.
    /// </summary>
    private List<MigrationStep> CompareComputedColumns(EntityModel oldEntity, EntityModel newEntity)
    {
        var steps = new List<MigrationStep>();

        var oldComputed = oldEntity.ComputedColumns.ToDictionary(c => c.Name);
        var newComputed = newEntity.ComputedColumns.ToDictionary(c => c.Name);

        foreach (var newCol in newComputed.Values)
        {
            if (!oldComputed.TryGetValue(newCol.Name, out var oldCol))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.AddColumn,
                    EntityName = newEntity.Name,
                    Schema = newEntity.Schema,
                    Description = $"Add computed column '{newCol.Name}'",
                    Metadata = new Dictionary<string, object> { ["Column"] = newCol }
                });
            }
            else if (ColumnChanged(oldCol, newCol))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.AlterColumn,
                    EntityName = newEntity.Name,
                    Schema = newEntity.Schema,
                    Description = $"Alter computed column '{newCol.Name}'",
                    Metadata = new Dictionary<string, object>
                    {
                        ["OldColumn"] = oldCol,
                        ["NewColumn"] = newCol
                    }
                });
            }
        }

        foreach (var oldCol in oldComputed.Values)
        {
            if (!newComputed.ContainsKey(oldCol.Name))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.DropColumn,
                    EntityName = oldEntity.Name,
                    Schema = oldEntity.Schema,
                    Description = $"Drop computed column '{oldCol.Name}'",
                    Metadata = new Dictionary<string, object> { ["Column"] = oldCol }
                });
            }
        }

        return steps;
    }

    /// <summary>
    /// Compares indexes between two entity models.
    /// </summary>
    private List<MigrationStep> CompareIndexes(EntityModel oldEntity, EntityModel newEntity)
    {
        var steps = new List<MigrationStep>();

        var oldIndexes = ExtractIndexes(oldEntity).ToDictionary(i => i.Name);
        var newIndexes = ExtractIndexes(newEntity).ToDictionary(i => i.Name);

        foreach (var newIndex in newIndexes.Values)
        {
            if (!oldIndexes.TryGetValue(newIndex.Name, out var oldIndex))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.AddIndex,
                    EntityName = newEntity.Name,
                    Schema = newEntity.Schema,
                    Description = $"Add index '{newIndex.Name}'",
                    Metadata = new Dictionary<string, object> { ["Index"] = newIndex }
                });
            }
            else if (IndexChanged(oldIndex, newIndex))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.AlterIndex,
                    EntityName = newEntity.Name,
                    Schema = newEntity.Schema,
                    Description = $"Alter index '{newIndex.Name}'",
                    Metadata = new Dictionary<string, object>
                    {
                        ["OldIndex"] = oldIndex,
                        ["NewIndex"] = newIndex
                    }
                });
            }
        }

        foreach (var oldIndex in oldIndexes.Values)
        {
            if (!newIndexes.ContainsKey(oldIndex.Name))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.DropIndex,
                    EntityName = oldEntity.Name,
                    Schema = oldEntity.Schema,
                    Description = $"Drop index '{oldIndex.Name}'",
                    Metadata = new Dictionary<string, object> { ["Index"] = oldIndex }
                });
            }
        }

        return steps;
    }

    /// <summary>
    /// Compares check constraints between two entity models.
    /// </summary>
    private List<MigrationStep> CompareCheckConstraints(EntityModel oldEntity, EntityModel newEntity)
    {
        var steps = new List<MigrationStep>();

        var oldChecks = oldEntity.Constraints.ToHashSet();
        var newChecks = newEntity.Constraints.ToHashSet();

        foreach (var check in newChecks)
        {
            if (!oldChecks.Contains(check))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.AddConstraint,
                    EntityName = newEntity.Name,
                    Schema = newEntity.Schema,
                    Description = $"Add check constraint '{check}'",
                    Metadata = new Dictionary<string, object> { ["Constraint"] = check }
                });
            }
        }

        foreach (var check in oldChecks)
        {
            if (!newChecks.Contains(check))
            {
                steps.Add(new MigrationStep
                {
                    Operation = MigrationOperation.DropConstraint,
                    EntityName = oldEntity.Name,
                    Schema = oldEntity.Schema,
                    Description = $"Drop check constraint '{check}'",
                    Metadata = new Dictionary<string, object> { ["Constraint"] = check }
                });
            }
        }

        return steps;
    }

    /// <summary>
    /// Extracts all index models from both column-level and table-level definitions.
    /// Ensures uniqueness and proper column mapping.
    /// </summary>
    /// <param name="entity">The entity model to extract indexes from.</param>
    /// <returns>A list of consolidated index models.</returns>
    private List<IndexModel> ExtractIndexes(EntityModel entity)
    {
        var indexGroups = new Dictionary<string, IndexModel>();

        // Column-level indexes
        foreach (var column in entity.Columns)
        {
            foreach (var index in column.Indexes)
            {
                if (!indexGroups.TryGetValue(index.Name, out var existing))
                {
                    existing = new IndexModel
                    {
                        Name = index.Name,
                        IsUnique = index.IsUnique,
                        Columns = new List<string>()
                    };
                    indexGroups[index.Name] = existing;
                }

                if (!existing.Columns.Contains(column.Name))
                    existing.Columns.Add(column.Name);
            }
        }

        // Table-level indexes
        foreach (var index in entity.TableIndexes)
        {
            if (!indexGroups.ContainsKey(index.Name))
            {
                indexGroups[index.Name] = new IndexModel
                {
                    Name = index.Name,
                    IsUnique = index.IsUnique,
                    Columns = index.Columns.ToList()
                };
            }
        }

        return indexGroups.Values.ToList();
    }

    /// <summary>
    /// Determines whether two column models differ in schema-relevant properties.
    /// </summary>
    /// <param name="oldCol">The original column model.</param>
    /// <param name="newCol">The updated column model.</param>
    /// <returns>True if the column has changed and requires migration.</returns>
    private bool ColumnChanged(ColumnModel oldCol, ColumnModel newCol)
    {
        return
            oldCol.TypeName != newCol.TypeName ||
            oldCol.IsNullable != newCol.IsNullable ||
            oldCol.DefaultValue?.ToString() != newCol.DefaultValue?.ToString() ||
            oldCol.Collation != newCol.Collation ||
            oldCol.IsComputed != newCol.IsComputed ||
            oldCol.ComputedExpression != newCol.ComputedExpression ||
            oldCol.IsPersisted != newCol.IsPersisted ||
            oldCol.IsUnique != newCol.IsUnique ||
            oldCol.UniqueConstraintName != newCol.UniqueConstraintName ||
            oldCol.IsPrimaryKey != newCol.IsPrimaryKey ||
            oldCol.IsForeignKey != newCol.IsForeignKey ||
            oldCol.ForeignKeyTarget != newCol.ForeignKeyTarget;
    }

    /// <summary>
    /// Determines whether two index models differ in uniqueness or column composition.
    /// </summary>
    /// <param name="oldIndex">The original index model.</param>
    /// <param name="newIndex">The updated index model.</param>
    /// <returns>True if the index has changed and requires migration.</returns>
    private bool IndexChanged(IndexModel oldIndex, IndexModel newIndex)
    {
        return
            oldIndex.IsUnique != newIndex.IsUnique ||
            !oldIndex.Columns.SequenceEqual(newIndex.Columns);
    }
}