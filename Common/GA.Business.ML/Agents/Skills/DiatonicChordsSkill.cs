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
[GuitarAlchemist.Registry.GaSkill("DiatonicChords", "chord")]
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
        // Bare "Diatonic chords in <KEY>" phrasings — without these, the
        // SemanticIntentRouter scored ChordSubstitutionSkill higher (which
        // has "Modal interchange substitutes for C major" + "Alternative
        // chord for F major in C") because the "in <KEY>" tail dominates
        // similarity. Caught 2026-05-13 via live UI click-test on
        // "Diatonic chords in G major" → 94% misroute to substitution.
        "Diatonic chords in G major",
        "Diatonic chords in D major",
        "Diatonic chords in F major",
        "Diatonic chords in E minor",
        "Show me diatonic chords in F# major",
        "Seven diatonic chords of B major",
        // PR (2026-05-30) — Roman-numeral "progression in <key>" surface. Live bug:
        // "give me a ii-V-I progression in Bb" misrouted to tab.optimize because that
        // intent's example "make this progression smoother to play" was the nearest
        // centroid (the word "progression" dominated) and this skill had NO progression
        // example. "ii-V-I in Bb" (terse form) already routed here correctly and returns
        // Cm F Bb = degrees ii/V/I of domain.diatonicChords. These add the verbose
        // "progression in <key>" phrasings only — deliberately paired with "in <key>"
        // (which this skill owns) and NOT bare "ii-V-I" (which collides with the
        // chord-substitution surface "substitute … in a ii-V-I"). Pairs with a
        // ProductionOrchestrator guard that suppresses tab intents on tabless queries.
        "Give me a ii-V-I progression in Bb",
        "ii-V-I progression in C major",
        "I-IV-V progression in G",
        "Give me a I-vi-IV-V progression in C",
    ];
}
