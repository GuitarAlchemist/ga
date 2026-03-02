namespace GA.Business.ML.Agents;

using Microsoft.Extensions.AI;

/// <summary>
///     Agent specialized in guitar tablature parsing and analysis.
/// </summary>
/// <remarks>
///     <para>
///         The Tab Agent handles queries about:
///         <list type="bullet">
///             <item>ASCII tab parsing and chord extraction</item>
///             <item>Tab-to-pitch conversion</item>
///             <item>Fret position analysis</item>
///             <item>Timing and rhythm interpretation</item>
///             <item>Tab notation conventions</item>
///         </list>
///     </para>
/// </remarks>
public class TabAgent(IChatClient chatClient, ILogger<TabAgent> logger) : GuitarAlchemistAgentBase(chatClient, logger)
{
    public override string AgentId => AgentIds.Tab;
    public override string Name => "Tab Agent";

    public override string Description =>
        "Parses and analyzes guitar tablature. Extracts chords, timing, and position information " +
        "from ASCII tab notation. Converts tab to pitch classes and MIDI notes.";

    public override IReadOnlyList<string> Capabilities => new[]
    {
        "ASCII tab parsing",
        "Chord extraction from tab",
        "Tab-to-pitch conversion",
        "Fret position analysis",
        "Timing interpretation",
        "Tab notation validation",
        "Multi-track tab reading",
        "Tab simplification"
    };

    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("TabAgent processing: {Query}", request.Query);

        // Check if the query contains actual tab notation for better steering
        var containsTab = ContainsTabNotation(request.Query);

        var prompt = BuildTabPrompt(request, containsTab);
        var responseText = await ChatAsync(request.Query, prompt, cancellationToken);

        return ParseStructuredResponse(responseText, "Tab parsing failed.");
    }

    private string BuildTabPrompt(AgentRequest request, bool containsTab)
    {
        var basePrompt = BuildSystemPrompt();

        var tabSpecificGuidance = containsTab
            ? """

              The user has provided guitar tablature. When parsing:
              1. Standard tuning is E-A-D-G-B-E (low to high) unless otherwise specified
              2. Numbers indicate fret positions (0 = open string)
              3. 'x' means muted/not played, 'h' is hammer-on, 'p' is pull-off
              4. '/' and '\' indicate slides, 'b' is bend
              5. Extract all simultaneous notes as chords
              6. Note the fret span (stretch) required
              """
            : """

              Help the user understand guitar tablature concepts or notation.
              """;

        var contextAddition = "";
        if (!string.IsNullOrEmpty(request.Context))
        {
            contextAddition = $"\n\nTuning/Context:\n{request.Context}";
        }

        return basePrompt + tabSpecificGuidance + contextAddition + """


                                                                    IMPORTANT: Your response MUST be a valid JSON object matching this structure:
                                                                    {
                                                                      "result": "Your detailed tab analysis and interpretation here...",
                                                                      "confidence": 0.90,
                                                                      "evidence": ["Fret detected on string X", "MIDI note Y extracted"],
                                                                      "assumptions": ["Standard tuning assumed"],
                                                                      "data": {
                                                                        "extractedChords": [],
                                                                        "pitchClasses": [],
                                                                        "midiNotes": []
                                                                      }
                                                                    }

                                                                    Include pitch classes in set notation like {0, 4, 7} and MIDI notes if derivable in the data field.
                                                                    """;
    }

    private static bool ContainsTabNotation(string query)
    {
        // Simple heuristics to detect tab (still useful for prompt choice)
        var lines = query.Split('\n');

        // Tab usually has 6 lines with consistent fret numbers
        var tabPatterns = new[] { "e|", "B|", "G|", "D|", "A|", "E|", "|-", "-|" };
        var digitDashPattern = lines.Any(l => l.Contains("---") || l.Contains("-0-") || l.Contains("-1-"));
        var stringIndicator = lines.Any(l => tabPatterns.Any(p => l.Contains(p, StringComparison.OrdinalIgnoreCase)));

        return digitDashPattern || stringIndicator;
    }
}
