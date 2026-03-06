namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using GA.Infrastructure.Documentation;

/// <summary>
/// Generates system prompts enriched with domain vocabulary to help the LLM understand valid search filters.
/// Part of Phase 1 (Hybrid Search).
/// </summary>
public class DomainMetadataPrompter(SchemaDiscoveryService schemaService)
{
    public string BuildSystemPrompt()
    {
        var voc = schemaService.GetDomainVocabulary();
        var sb = new StringBuilder();

        sb.AppendLine("SYSTEM: You are a query understanding engine for the Guitar Alchemist Hybrid Search system.");
        sb.AppendLine("Your goal is to extract structured filters from the user's natural language query.");
        sb.AppendLine();

        sb.AppendLine("### VALID DOMAIN VOCABULARY ###");
        sb.AppendLine("You can ONLY extract filters that strictly match the following values:");

        sb.AppendLine("1. CHORD QUALITIES (Field: 'Quality'):");
        sb.AppendLine($"   [{string.Join(", ", voc.ChordQualities)}]");
        sb.AppendLine("   - Example: 'sad minor chords' -> Quality='Minor'");
        sb.AppendLine("   - Example: 'dreamy major 7' -> Quality='Major'");

        sb.AppendLine("2. EXTENSIONS (Field: 'Extension'):");
        sb.AppendLine($"   [{string.Join(", ", voc.Extensions)}]");
        sb.AppendLine("   - Example: 'jazzy 13th chords' -> Extension='13'");
        sb.AppendLine("   - Example: 'basic triads' -> Extension='None' (or omit)");

        sb.AppendLine("3. STACKING TYPES (Field: 'StackingType'):");
        sb.AppendLine($"   [{string.Join(", ", voc.StackingTypes)}]");
        sb.AppendLine("   - Example: 'quartal harmony' -> StackingType='Quartal'");
        sb.AppendLine("   - Example: 'cluster voicings' -> StackingType='Cluster'");

        sb.AppendLine("4. INTENT (Field: 'Intent'):");
        sb.AppendLine("   Identify the user's primary goal:");
        sb.AppendLine("   - 'OptimizePath': User asks for a smoother path, better voice leading, or physical optimization of a progression (e.g., 'Suggest a smoother path for C to G', 'Optimize this tab', 'Easier way to play').");
        sb.AppendLine("   - 'AnalyzeTab': User provides a tab and wants general analysis, key detection, or style.");
        sb.AppendLine("   - 'Compare': User wants to compare the distance or difference between two chords.");
        sb.AppendLine("   - 'Search': User is just looking for a specific chord or voicing (default).");
        sb.AppendLine();

        sb.AppendLine("### OUTPUT FORMAT ###");
        sb.AppendLine("Return a JSON object with the extracted filters. Omit fields if not specified.");
        sb.AppendLine("Example Output: { \"Intent\": \"OptimizePath\", \"Quality\": \"Minor\", \"Extension\": \"7\" }");

        return sb.ToString();
    }
}
