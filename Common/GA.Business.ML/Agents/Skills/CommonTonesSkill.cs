namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Finds the notes shared between two chords and describes their interval
/// role in each. Second Path B canary (Phase 2b — transpose was first).
/// The inherited <see cref="SkillMdDrivenWrapperBase"/> owns Lazy plumbing,
/// exception handling, and Evidence stamping; this class only declares
/// routing metadata + the closure target.
/// </summary>
public sealed class CommonTonesSkill(
    IMcpToolsProvider toolsProvider,
    IChatClientFactory chatClientFactory,
    ILoggerFactory loggerFactory)
    : SkillMdDrivenWrapperBase(toolsProvider, chatClientFactory, loggerFactory)
{
    protected override string SkillFolderName       => "common-tones";
    protected override string ResponseAgentId       => AgentIds.Theory;
    protected override string ClosureName           => "domain.commonTones";
    protected override string DegradedResponseText  => "I couldn't compute the common tones for those chords right now. Please try again.";

    public override string Name        => "CommonTones";

    public override string Description =>
        "Finds notes shared between two chords and describes their interval role " +
        "in each (root / 3rd / 5th / 7th / extension). Used for pivot-chord choice, " +
        "smooth voice leading, and modulation prep. Routes through ga_dsl_eval to " +
        "the domain.commonTones closure; the closure handles the role-mapping that " +
        "LLMs fumble on extended/altered chords.";

    public override IReadOnlyList<string> ExamplePrompts =>
    [
        "What notes do Cmaj7 and Am7 share?",
        "Common tones between G and D7",
        "What's shared between Cmaj9 and Em7?",
        "Pivot tones from F to Bb7",
        "Notes in common between Dm7 and G7",
        "What do C major and A minor share?",
        // "Overlapping notes" / "overlap" pattern — was losing to ChordInfoSkill
        // because two chord names dominated the embedding without an anchor on
        // the shared-notes concept. The verb "overlap" is the discriminator.
        // Added 2026-05-12 to close ct-5 misroute.
        "Overlapping notes in Cmaj7 and Em7",
        "Which notes overlap between Dm7 and G7?",
        "Find the overlap of F major and Am7",
    ];
}
