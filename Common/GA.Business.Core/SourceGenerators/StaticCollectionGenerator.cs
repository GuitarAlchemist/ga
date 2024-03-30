using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GA.Business.Core.SourceGenerators;

[Generator]
public class StaticCollectionGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Find all classes implementing IStaticReadonlyCollection
        var syntaxTrees = context.Compilation.SyntaxTrees;
        var classes = new List<ClassDeclarationSyntax>();

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(tree);
            var classNodes = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classNode in classNodes)
            {
                if (semanticModel.GetDeclaredSymbol(classNode) is not ITypeSymbol typeSymbol) continue;
                
                if (typeSymbol.Interfaces.Any(i => i.ToDisplayString() == "YourNamespace.IStaticReadonlyCollection"))
                {
                    classes.Add(classNode);
                }
            }
        }

        // Generate the static class containing all items
        var sb = new StringBuilder();
        sb.AppendLine("public static class AllStaticCollections");
        sb.AppendLine("{");

        foreach (var cls in classes)
        {
            sb.AppendLine($"    public static readonly {cls.Identifier.Text} {cls.Identifier.Text}Instance = new {cls.Identifier.Text}();");
        }

        sb.AppendLine("}");

        context.AddSource("AllStaticCollections.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}