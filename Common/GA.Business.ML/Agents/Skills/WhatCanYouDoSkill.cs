namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// The chatbot's self-disclosure / capabilities meta-skill. Lists what the
/// chatbot can do (chord/scale lookup, progression analysis, voicing search,
/// etc.) with one-line examples. Catalog skill (see <see cref="CatalogSkillBase"/>):
/// body is loaded from <c>skills/what-can-you-do/SKILL.md</c>, zero LLM calls.
/// </summary>
public sealed class WhatCanYouDoSkill(ILogger<WhatCanYouDoSkill> logger) : CatalogSkillBase(logger)
{
    public override string Name        => "WhatCanYouDo";
    public override string Description =>
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
    public override IReadOnlyList<string> ExamplePrompts =>
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

    protected override string FolderName => "what-can-you-do";
    protected override string Fallback   =>
        "Guitar Alchemist's chatbot answers grounded music-theory questions — chord and scale lookup, progression analysis, voice leading, transposition, voicing search, and more. Every answer is computed from the GA symbolic engine, not recalled from training data.";
}
