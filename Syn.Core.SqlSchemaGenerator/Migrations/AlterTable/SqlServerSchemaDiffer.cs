namespace Syn.Core.SqlSchemaGenerator.Migrations.AlterTable;

/// <summary>
/// SQL Server implementation of ISchemaDiffer.
/// Generates ALTER TABLE scripts based on schema differences.
/// </summary>
public class SqlServerSchemaDiffer : ISchemaDiffer
{
    /// <summary>
    /// Compares two schema snapshots and returns ALTER TABLE statements for SQL Server.
    /// </summary>
    public List<string> GenerateAlterTableScripts(SchemaSnapshot oldSchema, SchemaSnapshot newSchema)
    {
        var scripts = new List<string>();
        var oldColumns = oldSchema.Columns.ToDictionary(c => c.Name.ToLower());
        var newColumns = newSchema.Columns.ToDictionary(c => c.Name.ToLower());

        // Detect added columns
        foreach (var newCol in newColumns.Values)
        {
            if (!oldColumns.ContainsKey(newCol.Name.ToLower()))
            {
                scripts.Add($"ALTER TABLE [{newSchema.EntityName}] ADD [{newCol.Name}] {newCol.TypeName} {(newCol.IsNullable ? "NULL" : "NOT NULL")};");
            }
        }

        // Detect removed columns
        foreach (var oldCol in oldColumns.Values)
        {
            if (!newColumns.ContainsKey(oldCol.Name.ToLower()))
            {
                scripts.Add($"ALTER TABLE [{newSchema.EntityName}] DROP COLUMN [{oldCol.Name}];");
            }
        }

        // Detect modified columns (basic comparison)
        foreach (var newCol in newColumns.Values)
        {
            if (oldColumns.TryGetValue(newCol.Name.ToLower(), out var oldCol))
            {
                if (newCol.TypeName != oldCol.TypeName || newCol.IsNullable != oldCol.IsNullable)
                {
                    scripts.Add($"ALTER TABLE [{newSchema.EntityName}] ALTER COLUMN [{newCol.Name}] {newCol.TypeName} {(newCol.IsNullable ? "NULL" : "NOT NULL")};");
                }
            }
        }

        return scripts;
    }
}