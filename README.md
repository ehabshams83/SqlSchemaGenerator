📚 SqlSchemaGenerator – Full Project Documentation
📖 Overview
SqlSchemaGenerator is a .NET library for automating database schema generation, migrations, and EF Core model configuration. It’s designed for teams that want traceable, auditable, and reproducible schema changes without repetitive manual coding.

✨ Key Features
Entity-first design: Build schema from strongly-typed entity classes.

Automatic relationship mapping from metadata (RelationshipDefinition).

Index, constraint, and foreign key generation from ColumnDefinition.

Check constraints with EF Core 7+ API.

Multiple overloads for applying entity definitions from:

List<Type>

Single Assembly

Multiple Assemblies

Generic filters (1 or 2 types)

Flexible params Type[] filters

Full EF Core integration via OnModelCreating.

🏗️ Project Structure
Code
Syn.Core.SqlSchemaGenerator
 ├── AttributeHandlers
 ├── Attributes
 ├── Builders
 ├── Converters
 ├── Core
 ├── Deployment
 ├── Execution
 ├── Helper
 ├── Interfaces
 ├── Migrations
 ├── Models
 ├── Scanning
 ├── Sql
 ├── Storage
 ├── SchemaBuilder.cs
 └── Syn.Core.SqlSchemaGenerator.csproj
🔹 Core Components
Component	Purpose
ColumnDefinition	Describes a physical column (type, constraints, indexes, computed logic, metadata).
RelationshipDefinition	Describes relationships between entities (type, FK column, navigation properties, delete behavior).
EntityDefinitionBuilder	Builds entity definitions from CLR types, including relationships.
ApplyEntityDefinitionsToModel	Applies entity definitions to EF Core’s ModelBuilder.
TypeFiltering	Filters types from assemblies based on base classes or interfaces.
⚙️ ApplyEntityDefinitionsToModel – All Overloads
1. From List<Type>
csharp
ApplyEntityDefinitionsToModel(builder, new[] { typeof(Customer), typeof(Order) });
2. From Single Assembly
csharp
ApplyEntityDefinitionsToModel(builder, typeof(Customer).Assembly);
ApplyEntityDefinitionsToModel(builder, typeof(Customer).Assembly, typeof(BaseEntity), typeof(IAuditable));
3. From Multiple Assemblies
csharp
ApplyEntityDefinitionsToModel(builder, AppDomain.CurrentDomain.GetAssemblies());
ApplyEntityDefinitionsToModel(builder, AppDomain.CurrentDomain.GetAssemblies(), typeof(IMyEntityInterface));
4. Generic Filters
csharp
ApplyEntityDefinitionsToModel<IMyEntityInterface>(builder, typeof(Customer).Assembly);
ApplyEntityDefinitionsToModel<IMyEntityInterface, IAuditable>(builder, AppDomain.CurrentDomain.GetAssemblies());
5. Flexible params Type[]
csharp
ApplyEntityDefinitionsToModel(builder, typeof(Customer).Assembly, typeof(BaseEntity), typeof(IAuditable), typeof(ISoftDelete));
📊 Flow Diagram – Model Building
mermaid
flowchart TD
    A[Select Entity Types] -->|Manual List<Type>| B[ApplyEntityDefinitionsToModel]
    A2[From Assembly/Assemblies] -->|Filter by Base Class/Interface| B

    B --> C[EntityDefinitionBuilder.BuildAllWithRelationships]
    C --> D[Configure Columns]
    D --> E[Configure Indexes & Constraints]
    E --> F[Configure Relationships from RelationshipDefinition]
    F --> G[Configure Foreign Keys from ColumnDefinition]
    G --> H[Update ModelBuilder]
    H --> I[Ready for EF Core Migrations/Runtime]
💡 Best Practices
Keep ColumnDefinition and RelationshipDefinition metadata in sync with your entity classes.

Use params Type[] overload for maximum flexibility.

For large solutions, prefer multiple assemblies overload to keep configuration centralized.

Always review generated migrations before applying to production.

📌 Example – Full DbContext Integration
csharp
public class ApplicationDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all entities from current assembly, filtered by BaseEntity
        ApplyEntityDefinitionsToModel(builder, typeof(BaseEntity).Assembly, typeof(BaseEntity));
    }
}
🚀 Running Migrations with MigrationRunner
The MigrationRunner class allows you to execute migrations programmatically with full control over execution mode, preview, and impact analysis.

Example
csharp
// 1️⃣ Define the database connection string
var connectionString = DbConnectionString.connectionString;

// 2️⃣ Create the MigrationRunner instance
var runner = new MigrationRunner(connectionString);

// 3️⃣ Specify the entity types to include in the migration
var entityTypes = new List<Type>
{
    typeof(Customer),
    typeof(Order)
};

// 4️⃣ Run the migration with preview and impact analysis
runner.Initiate(
    entityTypes,
    execute: true,          // Execute after confirmation
    dryRun: false,          // Not a dry run — will execute if confirmed
    interactive: false,     // No per-command confirmation required
    previewOnly: true,      // Show the migration report before execution
    autoMerge: false,       // Do not auto-merge without review
    showReport: true,       // Display detailed migration report
    impactAnalysis: true    // Perform impact/risk analysis before execution
);
Parameter Explanation
Parameter	Type	Description
entityTypes	IEnumerable<Type>	The list of entity CLR types to include in the migration.
execute	bool	If true, the migration will be executed after confirmation.
dryRun	bool	If true, generates the migration script without executing it.
interactive	bool	If true, asks for confirmation before each SQL command.
previewOnly	bool	If true, shows the migration report without executing changes.
autoMerge	bool	If true, merges and applies changes automatically without manual review.
showReport	bool	If true, displays a detailed migration report in the console.
impactAnalysis	bool	If true, performs a risk/impact analysis before execution.
💡 Tip: Combine previewOnly: true with impactAnalysis: true to review the exact changes and their potential impact before committing them to the database.

📜 License
Choose a license (MIT) based on your distribution needs.