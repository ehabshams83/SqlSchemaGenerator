using Syn.Core.SqlSchemaGenerator.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TesterApp
{
    public static class RelationshipPrinter
    {
        public static void PrintRelationshipGraph(IEnumerable<EntityDefinition> entities)
        {
            Console.WriteLine("📊 Relationship Graph:");
            Console.WriteLine(new string('-', 40));

            foreach (var entity in entities)
            {
                foreach (var rel in entity.Relationships)
                {
                    string arrow = rel.Type switch
                    {
                        RelationshipType.OneToOne => "───1:1───>",
                        RelationshipType.OneToMany => "───1:N───>",
                        RelationshipType.ManyToOne => "───N:1───>",
                        RelationshipType.ManyToMany => "───N:N───>",
                        _ => "──────>"
                    };

                    string joinInfo = rel.Type == RelationshipType.ManyToMany
                        ? $" [JoinTable: {(rel.IsExplicitJoinEntity ? "Explicit" : "Auto")} '{rel.JoinEntityName}']"
                        : "";

                    Console.WriteLine($"{rel.SourceEntity} {arrow} {rel.TargetEntity}{joinInfo}");
                }
            }

            Console.WriteLine(new string('-', 40));
        }
    }
}
