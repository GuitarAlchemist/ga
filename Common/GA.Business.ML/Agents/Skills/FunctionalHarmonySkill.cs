namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Answers functional-harmony concept questions — why V7 resolves, tritone
/// substitution, secondary dominants, the Neapolitan, augmented sixths and
/// the cadence types. A reference skill: the LLM answers from the verified
/// <c>skills/functional-harmony/SKILL.md</c> body, with no closure to dispatch.
/// Added 2026-10-03 after the theory QA eval found these questions misrouted
/// to chord-lookup skills or to the ungrounded fallback, which got the
/// concepts wrong.
/// </summary>
[GuitarAlchemist.Registry.GaSkill("FunctionalHarmony", "theory")]
public sealed class FunctionalHarmonySkill(
    IMcpToolsProvider toolsProvider,
    IChatClientFactory chatClientFactory,
    ILoggerFactory loggerFactory)
    : SkillMdDrivenWrapperBase(toolsProvider, chatClientFactory, loggerFactory)
{
    protected override string SkillFolderName       => "functional-harmony";
    protected override string ResponseAgentId       => AgentIds.Theory;
    protected override string DegradedResponseText  => "I couldn't answer that harmony question right now. Please try again.";

    public override string Name        => "FunctionalHarmony";

    public override string Description =>
        "Explains how chords function and resolve in tonal harmony: why the " +
        "dominant seventh resolves, tendency tones, tritone substitution, " +
        "secondary dominants, the Neapolitan chord, augmented sixth chords, and " +
        "the cadence types (authentic, half, plagal, deceptive). Concept " +
        "questions answered from a verified reference, not chord lookups.";

    // Concept phrasings ("what does X mean", "why", "how does it resolve").
    // Requests that name a chord to substitute or two chords to voice-lead
    // stay with ChordSubstitution and VoiceLeading, whose examples cover
    // those shapes. No generic "how do secondary dominants work?" example:
    // it out-scored ChordSubstitution on "secondary dominant for ii" (cs-9),
    // so the secondary-dominant anchors are the V-of-V-in-a-key shape only.
    public override IReadOnlyList<string> ExamplePrompts =>
    [
        "Why do dominant seventh chords need resolution?",
        "Why is a dominant 7th chord so tense?",
        "What does tritone substitution mean?",
        "Which secondary dominant leads to the V chord in F major?",
        "Which chord is V of V in G major?",
        "Explain the Neapolitan sixth chord",
        "What is the flat two chord in a minor key?",
        "How do Italian, French and German sixth chords resolve?",
        "What are augmented sixth chords used for?",
        "What are the different types of cadences?",
        "How is a plagal cadence different from an authentic cadence?",
        "Explain the deceptive cadence V to vi",
        "What is a Phrygian half cadence?",
        "Where do the tendency tones of a dominant seventh resolve?",
        "In a D7 to G resolution, where do F# and C go?",
        "When V7 goes to I, where do the 3rd and 7th of the dominant move?",
    ];
}
