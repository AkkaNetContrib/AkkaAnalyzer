using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using System.IO;

namespace RoslynWorkspace
{
    class ClassReferenceCounter : Rewriter
    {
        public override async Task<Document> Rewrite(Document document)
        {
            var root = await document.GetSyntaxRootAsync();

            var editor = await DocumentEditor.CreateAsync(document);
            var semanticModel = await document.GetSemanticModelAsync();

            var classes = root
                .DescendantNodes(node => true)
                .OfType<ClassDeclarationSyntax>()
                .ToList();            

            foreach (var @class in classes)
            {
                var semanticClass = semanticModel.GetDeclaredSymbol(@class) as ITypeSymbol;

                //ignore static classes for now, they can yield 0 references even though they are used, if the usage is via extension methods.
                if (semanticClass.IsStatic)
                    continue;

                //ignore console app start programs
                if (semanticClass.Name == "Program")
                    continue;

                //HACK: ignore tests/specs
                if (semanticClass.Name.Contains("Spec") || semanticClass.BaseType != null && semanticClass.BaseType.Name.Contains("Spec"))
                    continue;

                if (semanticClass.Name.Contains("Test") || semanticClass.BaseType != null && semanticClass.BaseType.Name.Contains("Test"))
                    continue;

                if (semanticClass.ContainingNamespace.ToDisplayString().Contains("Test") || semanticClass.ContainingNamespace.ToDisplayString().Contains("Spec"))
                    continue;

                var references = await SymbolFinder.FindReferencesAsync(semanticClass, document.Project.Solution);
                
                var referenceCount = (from reference in references
                                      from locaton in reference.Locations
                                      select locaton).Count();

                if (referenceCount == 0)
                {
                    var fullClassName = semanticClass.ContainingNamespace.ToDisplayString() + "." + semanticClass.Name;

                    var configs = (from project in document.Project.Solution.Projects
                                   from doc in project.Documents
                                   where doc.FilePath != document.FilePath
                                   select doc).ToList();

                    bool usedInConfigs = false;
                    foreach(var other in configs)
                    {
                        if (other.GetTextAsync().Result.ToString().Contains(fullClassName))
                        {
                            usedInConfigs = true;
                            break;
                        }
                    }

                    foreach (string file in Directory.EnumerateFiles(Path.GetDirectoryName(document.Project.Solution.FilePath), "*.conf", SearchOption.AllDirectories))
                    {
                        var text = File.ReadAllText(file);
                        if (text.Contains(fullClassName))
                        {
                            usedInConfigs = true;
                            break;
                        }
                    }

                        if (usedInConfigs)
                    {
                        //this type is used in a config.
                        //TODO: the current code only checks if the full class name is present in another file.
                        //it does not take into account if the occurance is a comment or a string
                        continue;
                    }



                    const string comment = "/*TODO: this class is not used*/";
                    var newTrivia = SyntaxFactory.ParseLeadingTrivia(comment);
                    var newClass = @class.WithLeadingTrivia(@class.GetLeadingTrivia().Union(newTrivia));
                    editor.ReplaceNode(@class, newClass);
                }
            }

            //HACK: no idea why this fails in some cases. just ignore the changes to the current file and return the original doc.
            try
            {
                return editor.GetChangedDocument();
            }
            catch
            {
                return document;
            }
        }
    }
}
