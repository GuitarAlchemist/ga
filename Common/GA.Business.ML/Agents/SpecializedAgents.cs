namespace GA.Business.ML.Agents;

using Microsoft.Extensions.AI;

/// <summary>
///     Agent specialized in guitar playing technique and ergonomics.
/// </summary>
public class TechniqueAgent(IChatClient chatClient, ILogger<TechniqueAgent> logger)
    : GuitarAlchemistAgentBase(chatClient, logger)
{
    public override string AgentId => AgentIds.Technique;
    public override string Name => "Technique Agent";

    public override string Description =>
        "Evaluates guitar fingerings, suggests ergonomic alternatives, validates playability, " +
        "and provides technique-focused advice for chord voicings and passages.";

    public override IReadOnlyList<string> Capabilities =>
    [
        "Fingering validation",
        "Ergonomic analysis",
        "Alternative voicing suggestions",
        "Barre chord optimization",
        "Stretch assessment",
        "Hand position analysis",
        "Playability scoring"
    ];

    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("TechniqueAgent processing: {Query}", request.Query);

        var prompt = BuildTechniquePrompt(request);
        var responseText = await ChatAsync(request.Query, prompt, cancellationToken);

        return ParseStructuredResponse(responseText, "Technique evaluation failed.");
    }

    private string BuildTechniquePrompt(AgentRequest _) => BuildSystemPrompt() + """


        When analyzing technique:
        1. Consider the fret span (typically max 4-5 frets comfortably)
        2. Note finger assignments (1=index, 2=middle, 3=ring, 4=pinky)
        3. Identify barre requirements
        4. Consider string skipping difficulty
        5. Evaluate transition smoothness between positions
        6. Rate playability on a scale of 1-10 (1=easy, 10=virtuoso level).

        Always suggest easier alternatives when possible.

        Return your response as a JSON object matching this schema:
        {
          "result": "Detailed technique analysis and suggestions",
          "confidence": 0.95,
          "evidence": ["Fret span is 5 frets", "Requires pinky stretch"],
          "assumptions": ["Standard 6-string guitar"],
          "data": { "playabilityScore": 7, "barreRequired": true }
        }
        """;

    // Removed legacy heuristic methods in favor of ParseStructuredResponse
}

/// <summary>
///     Agent specialized in musical composition and reharmonization.
/// </summary>
public class ComposerAgent(IChatClient chatClient, ILogger<ComposerAgent> logger)
    : GuitarAlchemistAgentBase(chatClient, logger)
{
    public override string AgentId => AgentIds.Composer;
    public override string Name => "Composer Agent";

    public override string Description =>
        "Creates musical variations, reharmonizations, and generates chord progressions. " +
        "Uses phase sphere analysis for musically coherent transformations.";

    public override IReadOnlyList<string> Capabilities =>
    [
        "Reharmonization",
        "Chord substitution",
        "Variation generation",
        "Progression creation",
        "Modal interchange",
        "Tritone substitution"
    ];

    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("ComposerAgent processing: {Query}", request.Query);

        // When composing over a specific key, delegate to TheoryAgent for scale analysis
        if (Coordinator is not null && MentionsKey(request.Query))
        {
            Logger.LogInformation("ComposerAgent delegating key analysis to TheoryAgent");
            var theoryResult = await DelegateToAsync(
                $"Analyze the scales and modes available in this context: {request.Query}",
                AgentIds.Theory, cancellationToken);

            if (theoryResult.Confidence > 0.5f)
            {
                var enrichedQuery = $"{request.Query}\n\n[Theory context]: {theoryResult.Result}";
                var prompt = BuildComposerPrompt(request);
                var responseText = await ChatAsync(enrichedQuery, prompt, cancellationToken: cancellationToken);
                return ParseStructuredResponse(responseText, "Composition generation failed.");
            }
        }

        var composerPrompt = BuildComposerPrompt(request);
        var text = await ChatAsync(request.Query, composerPrompt, cancellationToken: cancellationToken);
        return ParseStructuredResponse(text, "Composition generation failed.");
    }

    private static bool MentionsKey(string query)
    {
        var q = query.ToLowerInvariant();
        return q.Contains(" key of ") || q.Contains(" in c ") || q.Contains(" in g ") ||
               q.Contains(" in d ") || q.Contains(" in a ") || q.Contains(" in e ") ||
               q.Contains(" in f ") || q.Contains(" in b ") || q.Contains("major") ||
               q.Contains("minor");
    }

    private string BuildComposerPrompt(AgentRequest _) => BuildSystemPrompt() + """


        When composing or reharmonizing:
        1. Maintain voice leading principles
        2. Consider functional harmony (T-S-D movement)
        3. Suggest multiple alternatives with different moods/styles
        4. Explain the harmonic reasoning behind changes
        5. Consider guitarist playability in suggestions

        Return your response as a JSON object matching this schema:
        {
          "result": "Detailed musical suggestions with reasoning",
          "confidence": 0.85,
          "evidence": ["Uses tritone substitution for V7", "Voice leading moves by step"],
          "assumptions": ["Western tonal harmony"],
          "data": { "suggestedChords": ["Cmaj7", "Db7", "Cmaj7"], "key": "C Major" }
        }
        """;

    // Removed legacy heuristic methods in favor of ParseStructuredResponse
}

/// <summary>
///     Agent specialized in critiquing and improving musical responses.
/// </summary>
public class CriticAgent(IChatClient chatClient, ILogger<CriticAgent> logger)
    : GuitarAlchemistAgentBase(chatClient, logger)
{
    public override string AgentId => AgentIds.Critic;
    public override string Name => "Critic Agent";

    public override string Description =>
        "Evaluates musical analyses for consistency, detects contradictions, " +
        "scores response quality, and suggests improvements.";

    public override IReadOnlyList<string> Capabilities =>
    [
        "Contradiction detection",
        "Consistency checking",
        "Quality scoring",
        "Improvement suggestions",
        "Fact verification"
    ];

    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("CriticAgent processing: {Query}", request.Query);

        var prompt = BuildCriticPrompt(request);
        var responseText = await ChatAsync(request.Query, prompt, cancellationToken);

        return ParseStructuredResponse(responseText, "Musical critique failed.");
    }

    private string BuildCriticPrompt(AgentRequest _) => BuildSystemPrompt() + """


                                                                              When critiquing:
                                                                              1. Look for internal contradictions
                                                                              2. Verify claims against music theory principles
                                                                              3. Score responses 1-10 for accuracy, completeness, and clarity
                                                                              4. Suggest specific improvements
                                                                              5. Be constructive and educational

                                                                              Return your response as a JSON object matching this schema:
                                                                              {
                                                                                "result": "Critical evaluation and improvements",
                                                                                "confidence": 0.9,
                                                                                "evidence": ["Identified enharmonic contradiction at line 4", "Missing secondary dominant analysis"],
                                                                                "assumptions": ["Strict tonal theory rules"],
                                                                                "data": { "accuracyScore": 8, "consistencyCheck": "passed" }
                                                                              }
                                                                              """;
}
