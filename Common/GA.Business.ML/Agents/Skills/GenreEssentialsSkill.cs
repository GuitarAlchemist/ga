namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// Explains the harmonic, melodic, rhythmic, and timbral signatures of common
/// genres (blues, jazz, pop, folk, modal, country, rock). Pure catalog skill,
/// zero LLM calls. Body is loaded from
/// <c>skills/genre-essentials/SKILL.md</c> so the markdown stays the single
/// source of truth.
/// </summary>
public sealed class GenreEssentialsSkill(ILogger<GenreEssentialsSkill> logger) : IOrchestratorSkill
{
    private const string SkillFolderName = "genre-essentials";

    private static readonly Lazy<string> _bodyCache = new(
        () => CatalogSkillMdLoader.LoadBodyOrFallback(
            SkillFolderName,
            "Genres are defined by harmony, melody, rhythm, and timbre. Blues uses 12-bar I7-IV7-V7 forms; jazz uses extended chords and ii-V-I cadences; pop favours diatonic 4-chord loops like I-V-vi-IV; modal music vamps on one chord with characteristic intervals."),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string Name        => "GenreEssentials";
    public string Description =>
        "Returns harmonic / melodic / rhythmic / timbral signatures for blues, " +
        "jazz, pop, folk, modal, country, and rock. Catalog answer; the user " +
        "learns what makes a genre sound like itself. No LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What makes blues sound like blues?",
        "What defines jazz harmony?",
        "How do I write a country progression?",
        "What's a typical pop chord progression?",
        "Tell me about modal sound",
        "What chords are common in folk music?",
    ];

    public bool CanHandle(string message) => false; // semantic-routing only

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var body = _bodyCache.Value;
        logger.LogDebug("GenreEssentialsSkill: returned {Length} chars", body.Length);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = body,
            Confidence = 1.0f,
            Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md"],
        });
    }
}
