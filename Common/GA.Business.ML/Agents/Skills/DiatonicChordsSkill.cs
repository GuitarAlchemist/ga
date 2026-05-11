namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Returns the seven diatonic chords of a key. Third Path B canary
/// (after transpose and common-tones). The inherited base owns Lazy
/// plumbing, exception handling, and Evidence stamping; this class
/// only declares routing metadata + the closure target.
/// </summary>
public sealed class DiatonicChordsSkill(
    IMcpToolsProvider toolsProvider,
    IChatClientFactory chatClientFactory,
    ILoggerFactory loggerFactory)
    : SkillMdDrivenWrapperBase(toolsProvider, chatClientFactory, loggerFactory)
{
    protected override string SkillFolderName       => "diatonic-chords";
    protected override string ResponseAgentId       => AgentIds.Theory;
    protected override string ClosureName           => "domain.diatonicChords";
    protected override string DegradedResponseText  => "I couldn't list the diatonic chords for that key right now. Please try again.";

    public override string Name        => "DiatonicChords";

    public override string Description =>
        "Lists the seven diatonic triads of a key — chord symbols ordered by " +
        "scale degree for major and minor keys. Examples: C major returns " +
        "C Dm Em F G Am B°; A minor returns Am B° C Dm Em F G. Routes through " +
        "ga_dsl_eval to the domain.diatonicChords closure so enharmonic " +
        "spelling is correct in less-common keys (Gb major, F# minor).";

    // PR (post-baseline-2026-05-11 corpus v0.4) — added the "list chords
    // in <key>" and "<degree> chord in <key>" surfaces that were
    // misrouting. Eval failures: "list the chords in G major" → was
    // routing to chordsubstitution; "what is the IV chord in A major" →
    // was routing to chordinfo. Both shapes are diatonic-chord queries —
    // the user wants the chords belonging to the key, identified by
    // degree or as a list.
    public override IReadOnlyList<string> ExamplePrompts =>
    [
        "Give me the diatonic chords in C major",
        "What are the seven chords in G major?",
        "Diatonic chords in A minor",
        "Chords of Bb major",
        "All chords in the key of D",
        "list the chords in G major",
        "what is the IV chord in A major",
        "what's the V chord in F",
        "ii chord of E minor",
        "what chord is the I in D major",
    ];
}
