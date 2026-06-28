namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Transposes a chord by a named musical interval. First Path B canary
/// (#146 / #151). The inherited base owns Lazy plumbing, exception
/// handling, and Evidence stamping; this class only declares routing
/// metadata + the closure target.
/// </summary>
[GuitarAlchemist.Registry.GaSkill("Transpose", "theory")]
public sealed class TransposeSkill(
    IMcpToolsProvider toolsProvider,
    IChatClientFactory chatClientFactory,
    ILoggerFactory loggerFactory)
    : SkillMdDrivenWrapperBase(toolsProvider, chatClientFactory, loggerFactory)
{
    protected override string SkillFolderName       => "transpose";
    protected override string ResponseAgentId       => AgentIds.Theory;
    protected override string ClosureName           => "domain.transposeChord";
    protected override string DegradedResponseText  => "I couldn't transpose that chord right now. Please try again.";

    public override string Name        => "Transpose";

    public override string Description =>
        "Transposes a chord by a named musical interval (e.g. Cmaj7 up a perfect " +
        "fourth = Fmaj7). Routes through ga_dsl_eval to the domain.transposeChord " +
        "closure; teaches the LLM the interval → semitones mapping in the body so " +
        "no enharmonic guessing happens at the model layer.";

    // PR (post-baseline-2026-05-11) — added the "transpose this PROGRESSION"
    // surface forms that the eval corpus uses. The prior single-chord
    // examples ("Transpose Cmaj7 up a perfect fourth") didn't move the
    // embedding score enough to overcome progressioncompletion's grip on
    // prompts like "transpose this progression down a half step". The
    // semantic-router score gap was wider than the +0.06 hint boost
    // could close, so the ExamplePrompts themselves need to teach the
    // embedder that progression-shaped transpose requests are this skill.
    public override IReadOnlyList<string> ExamplePrompts =>
    [
        "transpose this progression down a half step",
        "transpose this progression up a whole step",
        "transpose C-Am-F-G to G major",
        "shift this progression up a half step",
        "bring D minor down to A minor",
        "Transpose Cmaj7 up a perfect fourth",
        "Move this F chord down a minor third",
        "What's Dm7 up a whole step?",
        "Transpose G7 to Eb",
        "Shift Am7 up a fifth",
        // v0.5 corpus expansion (2026-05-12): "raise the key by N semitones"
        // pattern — was misrouting to skill.interval because the embedder
        // matched on "semitones". The key+raise/lower combo is the
        // transpose discriminator.
        "raise the key by two semitones",
        "lower the key by a half step",
        "transposing the chorus down a tone",
    ];
}
