namespace GA.Business.ML.Agents.AgentFramework;

using Microsoft.Extensions.AI;

/// <summary>
/// Wraps deterministic <see cref="IOrchestratorSkill"/> instances as
/// <see cref="AIFunction"/>s so a Microsoft Agent Framework <c>AIAgent</c> can
/// invoke them via tool-calling. This is the Phase 3 spike entry-point per the
/// migration recommendation §"Phase 3 — Agent Framework spike".
/// </summary>
/// <remarks>
/// <para>The agent does not lose the deterministic-fast-path guarantee: when an
/// <c>AIAgent</c> selects this tool, the underlying skill still computes the
/// answer purely from the GA domain — the LLM only invokes it. If the agent
/// chooses NOT to call the tool, the skill simply isn't run.</para>
/// <para>Provider DTOs do not leak: the wrapper accepts a <c>string</c> input
/// and returns a <c>string</c> answer (the skill's <c>Result</c>). The agent's
/// trace records the tool name and arguments, redactable by the chatbot's
/// observability hooks before persistence.</para>
/// </remarks>
public static class OrchestratorSkillTools
{
    /// <summary>
    /// Returns an <see cref="AIFunction"/> that an agent can call to invoke the
    /// supplied deterministic skill. The function name is
    /// <c>ga_skill_{Name lowercased}</c> so it cannot collide with MCP tool
    /// namespaces. The description is the skill's <c>Description</c>.
    /// </summary>
    public static AIFunction AsAIFunction(this IOrchestratorSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var name = $"ga_skill_{skill.Name.ToLowerInvariant().Replace(' ', '_')}";

        return AIFunctionFactory.Create(
            async (string message, CancellationToken ct) =>
            {
                var response = await skill.ExecuteAsync(message, ct);
                return response.Result;
            },
            name,
            skill.Description);
    }
}
