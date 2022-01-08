using Microsoft.CodeAnalysis;

namespace GA.Business.Core.Fretboard.Config;

/*
1) Source generator updates: incremental generators
https://andrewlock.net/exploring-dotnet-6-part-9-source-generator-updates-incremental-generators/

2) Roslyn cookbook
https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md
 */

[Generator]
public class AutoRegisterSourceGenerator : IIncrementalGenerator 
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // TODO
    }
}