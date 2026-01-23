namespace GA.Business.ML.Agents;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent specialized in music theory analysis: pitch classes, harmonic functions, chord analysis.
/// </summary>
/// <remarks>
/// <para>
/// The Theory Agent handles queries about:
/// <list type="bullet">
///   <item>Pitch class set analysis (OPTIC-K structure)</item>
///   <item>Harmonic function identification (tonic, dominant, subdominant)</item>
///   <item>Chord quality and extensions</item>
///   <item>Key relationships and modulations</item>
///   <item>Voice leading analysis</item>
/// </list>
/// </para>
/// </remarks>
public class TheoryAgent : GuitarAlchemistAgentBase
{
    public override string AgentId => AgentIds.Theory;
    public override string Name => "Theory Agent";
    public override string Description => 
        "Analyzes music theory concepts including pitch classes, harmonic functions, chord qualities, " +
        "key relationships, and voice leading. Expert in atonal and tonal theory.";
    
    public override IReadOnlyList<string> Capabilities => new[]
    {
        "Pitch class set analysis",
        "Harmonic function identification",
        "Chord quality determination",
        "Key and mode detection",
        "Voice leading evaluation",
        "Interval analysis",
        "Roman numeral analysis",
        "Cadence identification"
    };

    public TheoryAgent(IChatClient chatClient, ILogger<TheoryAgent> logger)
        : base(chatClient, logger)
    {
    }

    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("TheoryAgent processing: {Query}", request.Query);

        var prompt = BuildTheoryPrompt(request);
        var responseText = await ChatAsync(request.Query, prompt, cancellationToken);

        return ParseStructuredResponse(responseText, "Analysis failed.");
    }

    private string BuildTheoryPrompt(AgentRequest request)
    {
        var basePrompt = BuildSystemPrompt();
        
        var contextAddition = "";
        if (!string.IsNullOrEmpty(request.Context))
        {
            contextAddition = $"\n\nMusical Context:\n{request.Context}";
        }

        return basePrompt + contextAddition + """
            
            
            When analyzing music theory:
            1. Identify pitch classes using standard notation (C=0, C#/Db=1, ... B=11)
            2. Use Forte numbers for pitch class sets when applicable (e.g., 3-11 for major/minor triads)
            3. Explain harmonic function in relation to the key
            4. Note any voice leading considerations
            5. If multiple interpretations exist, list them with relative likelihood
            
            IMPORTANT: Your response MUST be a valid JSON object matching this structure:
            {
              "result": "Your detailed theory analysis here...",
              "confidence": 0.95,
              "evidence": ["Musical fact 1", "Musical fact 2"],
              "assumptions": ["Context assumption 1"],
              "data": null
            }
            """;
    }
}
