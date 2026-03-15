namespace GA.Business.DSL.SourceGen;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// MSBuild task: reads <c>*.ebnf</c> grammar files and emits <c>*.g.fs</c> F# source files.
/// Each generated file contains:
///   · A DU representing the grammar's AST (domain level)
///   · FParsec parser combinators that produce those DU values (grammar level)
///
/// Usage in .fsproj (via the companion .targets file):
///   <EbnfFiles Include="Grammars/*.ebnf" />
///   <OutputDirectory>Generated</OutputDirectory>
/// </summary>
public sealed class EbnfGeneratorTask : Task
{
    /// <summary>Input .ebnf files to process.</summary>
    [Required]
    public ITaskItem[] EbnfFiles { get; set; } = [];

    /// <summary>Directory where generated .g.fs files are written.</summary>
    [Required]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Generated file paths (output item for Compile inclusion).</summary>
    [Output]
    public ITaskItem[] GeneratedFiles { get; set; } = [];

    public override bool Execute()
    {
        if (!Directory.Exists(OutputDirectory))
            Directory.CreateDirectory(OutputDirectory);

        var generated = new List<ITaskItem>();
        var parser    = new EbnfParser();
        var success   = true;

        foreach (var item in EbnfFiles)
        {
            var inputPath = item.GetMetadata("FullPath");
            if (!File.Exists(inputPath))
            {
                Log.LogError($"EBNF file not found: {inputPath}");
                success = false;
                continue;
            }

            try
            {
                var grammarName = Path.GetFileNameWithoutExtension(inputPath);
                var outputPath  = Path.Combine(OutputDirectory, grammarName + ".g.fs");

                var source    = File.ReadAllText(inputPath);
                var rules     = parser.Parse(source);
                var emitter   = new FSharpEmitter(grammarName);
                var generated_ = emitter.Emit(rules);

                // Only write if content changed (avoids unnecessary rebuilds)
                var existing = File.Exists(outputPath) ? File.ReadAllText(outputPath) : null;
                if (existing != generated_)
                {
                    File.WriteAllText(outputPath, generated_);
                    Log.LogMessage(MessageImportance.Normal,
                        $"Generated {outputPath} from {grammarName}.ebnf ({rules.Count} rules)");
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, $"Skipped {grammarName}.ebnf (unchanged)");
                }

                generated.Add(new TaskItem(outputPath));
            }
            catch (Exception ex)
            {
                // Warn and skip grammars the parser can't handle yet (e.g. set difference).
                // Missing files are still hard errors (above), but parse failures are not.
                Log.LogWarning($"Skipping {Path.GetFileName(inputPath)}: {ex.Message}");
            }
        }

        GeneratedFiles = [.. generated];
        return success;
    }
}
