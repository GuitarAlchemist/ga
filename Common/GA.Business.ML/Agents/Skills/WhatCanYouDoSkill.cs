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

    // PR (post-baseline-2026-05-11 corpus v0.4) — expanded to cover the
    // help-discovery / capability-exploration surface that the labeled
    // corpus uses. Prior set led to 2/5 prompts ("help me figure out what
    // to ask", "show me what you know") returning conf=0.00 — no embedding
    // match at all, not even close. Lowering the router threshold didn't
    // help (verified at 0.60 — same failures). These prompts need to BE
    // examples to lift them above threshold.
    public IReadOnlyList<string> ExamplePrompts =>
    [
        "what can you do",
        "what can the chatbot do",
        "what are your capabilities",
        "what features do you have",
        "list the chatbot's features",
        "show me what you know",
        "help me figure out what to ask",
        "what do you know about music",
        "what can I ask",
        "how do I use the chatbot",
        // v0.5 corpus expansion (2026-05-12): conversational meta paraphrases.
        "I'm new here — what can you help with",
        "menu of available actions",
        "give me a tour of your features",
        "list your skills",
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
