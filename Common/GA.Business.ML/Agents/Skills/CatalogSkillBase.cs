namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// Deep base for <b>catalog skills</b>: their entire behaviour is "load a
/// markdown body from <c>skills/&lt;folder&gt;/SKILL.md</c> (with a hardcoded
/// fallback), cache it, and return it as a high-confidence
/// <see cref="AgentResponse"/>." Subclasses declare only the metadata that
/// varies — <see cref="Name"/>, <see cref="Description"/>,
/// <see cref="ExamplePrompts"/>, the <see cref="FolderName"/>, and the
/// <see cref="Fallback"/> text.
/// </summary>
/// <remarks>
/// The loader, the semantic-routing-only <see cref="CanHandle"/> convention,
/// and the response shape live here once — so the four catalog skills
/// (CircleOfFifths, PracticeRoutine, GenreEssentials, WhatCanYouDo) stop
/// copying an identical body. A catalog skill participates in routing only via
/// <see cref="ExamplePrompts"/> (embedding-first), never via keyword matching.
/// </remarks>
public abstract class CatalogSkillBase : IOrchestratorSkill
{
    private readonly ILogger _logger;
    private readonly Lazy<string> _bodyCache;

    protected CatalogSkillBase(ILogger logger)
    {
        _logger = logger;
        // FolderName / Fallback are abstract; the delegate runs lazily on first
        // ExecuteAsync (well after construction), so the virtual calls are safe.
        _bodyCache = new Lazy<string>(
            () => CatalogSkillMdLoader.LoadBodyOrFallback(FolderName, Fallback),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> ExamplePrompts { get; }

    /// <summary>The <c>skills/&lt;folder&gt;/SKILL.md</c> directory the body loads from.</summary>
    protected abstract string FolderName { get; }

    /// <summary>Body returned when the SKILL.md is missing or unparseable.</summary>
    protected abstract string Fallback { get; }

    /// <summary>Catalog skills route via semantic example-embeddings only.</summary>
    public bool CanHandle(string message) => false;

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var body = _bodyCache.Value;
        _logger.LogDebug("{Skill}: returned {Length} chars", Name, body.Length);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = body,
            Confidence = 1.0f,
            Evidence   = [$"Source: skills/{FolderName}/SKILL.md"],
        });
    }
}
