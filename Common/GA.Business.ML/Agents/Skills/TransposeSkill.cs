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

    public override IReadOnlyList<string> ExamplePrompts =>
    [
        "Transpose Cmaj7 up a perfect fourth",
        "Move this F chord down a minor third",
        "What's Dm7 up a whole step?",
        "Transpose G7 to Eb",
        "Shift Am7 up a fifth",
        "Cmaj7 in the key of G — what chord is that?",
    ];
}
