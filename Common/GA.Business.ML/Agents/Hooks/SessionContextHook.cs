namespace GA.Business.ML.Agents.Hooks;

using System.Text;
using GA.Business.Core.Context;

/// <summary>
/// Hook that prepends session context (skill level, key, genre) to the message
/// so agents can adapt their responses to the user's profile.
/// </summary>
public sealed class SessionContextHook(ILogger<SessionContextHook> logger) : IChatHook
{
    public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
    {
        var session = ctx.SessionContext;
        if (session is null)
            return Task.FromResult(HookResult.Continue);

        // Only inject context when there's meaningful information beyond defaults
        if (session.SkillLevel is null && session.CurrentKey is null && session.CurrentGenre is null)
            return Task.FromResult(HookResult.Continue);

        var block = new StringBuilder();
        block.AppendLine("[SESSION CONTEXT]");

        if (session.SkillLevel is { } level)
        {
            block.AppendLine($"Skill level: {level}");
            block.AppendLine(GetAdaptiveInstructions(level));
        }

        if (session.CurrentKey is { } key)
            block.AppendLine($"Current key: {key}");

        if (session.CurrentGenre is { } genre)
            block.AppendLine($"Genre: {genre}");

        if (session.CurrentScale is { } scale)
            block.AppendLine($"Current scale: {scale}");

        if (session.MasteredTechniques.Count > 0)
            block.AppendLine($"Mastered techniques: {string.Join(", ", session.MasteredTechniques)}");

        block.AppendLine("[/SESSION CONTEXT]");

        var enriched = $"{block}\n{ctx.CurrentMessage}";
        logger.LogDebug("SessionContextHook: injecting session context (level={Level})", session.SkillLevel);
        return Task.FromResult(HookResult.Mutate(enriched));
    }

    private static string GetAdaptiveInstructions(SkillLevel level) => level switch
    {
        SkillLevel.Beginner =>
            "Adapt your response for a beginner: use simple language, avoid jargon, " +
            "explain terms when first used, suggest easy exercises, and be encouraging.",
        SkillLevel.Intermediate =>
            "Adapt for an intermediate player: you can use standard music terminology, " +
            "reference common scales and chord shapes, and suggest moderately challenging exercises.",
        SkillLevel.Advanced =>
            "Adapt for an advanced player: use technical terminology freely, " +
            "include Roman numeral analysis, discuss voice leading, and suggest complex exercises.",
        SkillLevel.Expert =>
            "Adapt for an expert: assume deep theory knowledge, discuss advanced concepts like " +
            "set theory, Schenkerian analysis, or advanced harmony without simplification.",
        _ => string.Empty
    };
}
