namespace GA.Business.ML.Agents.AgentFramework;

using GA.Business.ML.Extensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

/// <summary>
/// Builds an experimental Microsoft Agent Framework <see cref="AIAgent"/> that
/// composes an <see cref="IChatClient"/> with a <see cref="FileBasedSkillsProvider"/>.
/// </summary>
/// <remarks>
/// This is the Phase 3 spike entry-point per the migration recommendation:
/// it wraps the existing chatbot stack as an Agent Framework agent without
/// touching <c>ProductionOrchestrator</c>. Output is mappable to the existing
/// <c>ChatResponse</c> + <c>AgentRoutingMetadata</c> DTOs by callers; this
/// builder does not impose a UI contract change.
/// </remarks>
public static class SkillsAgentBuilder
{
    /// <summary>
    /// Construct an agent for the supplied <paramref name="purpose"/>
    /// (resolved through <see cref="IChatClientFactory"/>) over the SKILL.md
    /// files in <paramref name="skillsDirectory"/>.
    /// </summary>
    /// <param name="chatClientFactory">Factory used to obtain an
    ///   <see cref="IChatClient"/> for <paramref name="purpose"/>.</param>
    /// <param name="purpose">One of the well-known purposes (default,
    ///   skill-md, qa-architect, fast-local).</param>
    /// <param name="skillsDirectory">Repo-rooted directory containing
    ///   <c>SKILL.md</c> files. Missing directory is permitted; the resulting
    ///   agent simply runs without skill context.</param>
    /// <param name="agentName">Optional logical name for the agent (used by
    ///   trace tags / DevUI).</param>
    /// <param name="instructions">Optional base instructions appended ahead of
    ///   per-skill context returned by the provider.</param>
    public static AIAgent Build(
        IChatClientFactory chatClientFactory,
        string purpose,
        string skillsDirectory,
        string agentName = "GaSkillsAgent",
        string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(chatClientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillsDirectory);

        var chatClient = chatClientFactory.Create(purpose);
        var skillsProvider = new FileBasedSkillsProvider(skillsDirectory);

        var options = new ChatClientAgentOptions
        {
            Name = agentName,
            ChatOptions = new ChatOptions
            {
                Instructions = instructions ?? "You are the Guitar Alchemist assistant.",
            },
            AIContextProviders = [skillsProvider],
        };

        return new ChatClientAgent(chatClient, options);
    }
}
