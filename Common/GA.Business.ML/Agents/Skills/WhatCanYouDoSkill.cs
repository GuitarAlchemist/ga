namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// The chatbot's self-disclosure / capabilities meta-skill. Lists what the
/// chatbot can do (chord/scale lookup, progression analysis, voicing search,
/// etc.) with one-line examples. Pure catalog skill, zero LLM calls. Body
/// is loaded from <c>skills/what-can-you-do/SKILL.md</c> so the markdown
/// stays the single source of truth.
/// </summary>
public sealed class WhatCanYouDoSkill(ILogger<WhatCanYouDoSkill> logger) : IOrchestratorSkill
{
    private const string SkillFolderName = "what-can-you-do";

    private static readonly Lazy<string> _bodyCache = new(
        () => CatalogSkillMdLoader.LoadBodyOrFallback(
            SkillFolderName,
            "Guitar Alchemist's chatbot answers grounded music-theory questions — chord and scale lookup, progression analysis, voice leading, transposition, voicing search, and more. Every answer is computed from the GA symbolic engine, not recalled from training data."),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string Name        => "WhatCanYouDo";
    public string Description =>
        "Lists the chatbot's currently-shipped capabilities — chord and scale " +
        "lookup, key identification, progression completion, voicing search, " +
        "interval computation, fret-span analysis. Discoverability skill for " +
        "first-time visitors. No LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What can you do?",
        "What can the chatbot do?",
        "How do I use the chatbot?",
        "What are your capabilities?",
        "What features do you have?",
        "List the chatbot's features",
    ];

    public bool CanHandle(string message) => false; // semantic-routing only

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var body = _bodyCache.Value;
        logger.LogDebug("WhatCanYouDoSkill: returned {Length} chars", body.Length);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = body,
            Confidence = 1.0f,
            Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md"],
        });
    }
}
