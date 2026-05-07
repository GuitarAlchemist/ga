namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using GA.Business.ML.Skills;
using Microsoft.Extensions.Logging;

/// <summary>
/// Returns the seven diatonic chords of a key (root + major/minor).
/// Path B canary (third — follows TransposeSkill and CommonTonesSkill):
/// the C# wrapper owns routing metadata; <c>ExecuteAsync</c> delegates to
/// a lazily-constructed <see cref="SkillMdDrivenSkill"/> that runs the
/// LLM-in-the-loop pass with <c>skills/diatonic-chords/SKILL.md</c> as
/// system prompt and <c>ga_dsl_eval</c>(<c>domain.diatonicChords</c>)
/// available as a tool.
/// </summary>
/// <remarks>
/// Adds a third Path B skill so the Phase 3 soak set has a concrete
/// target for "give me the diatonic chords in &lt;key&gt;" prompts —
/// previously those routed to <see cref="ChordInfoSkill"/> which can't
/// parse a key as a chord and 100% failed in the soak baseline.
/// </remarks>
public sealed class DiatonicChordsSkill : IOrchestratorSkill
{
    private const string SkillFolderName = "diatonic-chords";

    private readonly Lazy<SkillMdDrivenSkill> _inner;
    private readonly ILogger<DiatonicChordsSkill> _logger;

    public DiatonicChordsSkill(
        IMcpToolsProvider toolsProvider,
        IChatClientFactory chatClientFactory,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DiatonicChordsSkill>();
        var innerLogger = loggerFactory.CreateLogger<SkillMdDrivenSkill>();

        _inner = new Lazy<SkillMdDrivenSkill>(() =>
        {
            var path = Path.Combine(SkillMdPlugin.ResolveSkillsPath(), SkillFolderName, "SKILL.md");
            var skillMd = SkillMdParser.TryParse(path)
                ?? throw new InvalidOperationException(
                    $"DiatonicChordsSkill: skills/{SkillFolderName}/SKILL.md is missing or unparseable at {path}");
            return new SkillMdDrivenSkill(skillMd, toolsProvider, chatClientFactory, innerLogger);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Name        => "DiatonicChords";
    public string Description =>
        "Lists the seven diatonic triads of a key — chord symbols ordered by " +
        "scale degree for major and minor keys. Examples: C major returns " +
        "C Dm Em F G Am B°; A minor returns Am B° C Dm Em F G. Routes through " +
        "ga_dsl_eval to the domain.diatonicChords closure so enharmonic " +
        "spelling is correct in less-common keys (Gb major, F# minor).";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Give me the diatonic chords in C major",
        "What are the seven chords in G major?",
        "Diatonic chords in A minor",
        "Chords of Bb major",
        "Harmonic series of F# minor",
        "All chords in the key of D",
    ];

    public bool CanHandle(string message) => false; // semantic-routing only

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("DiatonicChordsSkill: delegating to SkillMdDrivenSkill (LLM + ga_dsl_eval)");

        try
        {
            var inner = await _inner.Value.ExecuteAsync(message, cancellationToken);
            return new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = inner.Result,
                Confidence = inner.Confidence,
                Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md", "Closure: domain.diatonicChords (via ga_dsl_eval)"],
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Lazy<T,ExecutionAndPublication> caches construction exceptions
            // forever; without this catch, a missing/unparseable
            // skills/diatonic-chords/SKILL.md would 500 every subsequent
            // request. Mirrors TransposeSkill (#151 review corr-2).
            _logger.LogError(ex, "DiatonicChordsSkill: inner SkillMdDrivenSkill failed");
            return new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = "I couldn't list the diatonic chords for that key right now. Please try again.",
                Confidence = 0f,
                Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md", "Closure: domain.diatonicChords (via ga_dsl_eval)"],
            };
        }
    }
}
