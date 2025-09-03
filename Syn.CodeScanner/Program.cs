using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Syn.CodeScanner
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            // 1. مسار الـ solution أو csproj
            var solutionPath = args.Length > 0
                ? args[0]
                : Path.Combine(Directory.GetCurrentDirectory(), "C:\\Users\\Ehab Shams\\source\\repos\\SqlSchemaGenerator\\SqlSchemaGenerator.sln");

            Console.WriteLine($"[Info] Loading solution: {solutionPath}");

            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            var sb = new StringBuilder();

            sb.AppendLine("# تقرير مسح SqlSchemaGenerator");
            sb.AppendLine();

            foreach (var project in solution.Projects)
            {
                sb.AppendLine($"## Project: {project.Name}");
                sb.AppendLine();

                // بدلاً من GetDocumentsAsync، نستخدم خاصية Documents
                IEnumerable<Document> allDocuments = project.Documents;

                foreach (var doc in allDocuments)
                {
                    var tree = await doc.GetSyntaxTreeAsync();
                    if (tree == null)
                        continue;

                    var root = await tree.GetRootAsync();

                    // نجمع كل الـ class و struct
                    var types = root.DescendantNodes()
                                    .OfType<TypeDeclarationSyntax>()
                                    .Where(t => t.Keyword.Text == "class" || t.Keyword.Text == "struct");

                    foreach (var type in types)
                    {
                        sb.AppendLine($"### {type.Keyword.Text} `{type.Identifier}`");

                        // الخصائص
                        var props = type.Members
                                        .OfType<PropertyDeclarationSyntax>()
                                        .Select(p => new
                                        {
                                            Name = p.Identifier.Text,
                                            Type = p.Type.ToString()
                                        });

                        if (props.Any())
                        {
                            sb.AppendLine("- **Properties:**");
                            foreach (var p in props)
                                sb.AppendLine($"  - `{p.Type}` {p.Name}");
                        }

                        // الدوال (Methods)
                        var methods = type.Members
                                          .OfType<MethodDeclarationSyntax>()
                                          .Select(m => m.Identifier.Text);

                        if (methods.Any())
                        {
                            sb.AppendLine("- **Methods:**");
                            foreach (var m in methods)
                                sb.AppendLine($"  - {m}()");
                        }

                        sb.AppendLine();
                    }
                }
            }

            // إخراج التقرير
            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "CodeSummary.md");
            File.WriteAllText(outPath, sb.ToString());
            Console.WriteLine($"[Done] Generated report at {outPath}");
        }
    }
}