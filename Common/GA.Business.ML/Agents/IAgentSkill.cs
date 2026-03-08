namespace GA.Business.ML.Agents;

/// <summary>
/// A discrete, named capability within an agent — handles a specific class of requests
/// using domain logic, then optionally calls the LLM for explanation.
/// </summary>
/// <remarks>
/// Mirrors the Claude Code skill pattern: each skill declares when it applies
/// (<see cref="CanHandle"/>) and what it does (<see cref="ExecuteAsync"/>).
/// Skills are checked in order before the agent's generic LLM fallback.
/// </remarks>
public interface IAgentSkill
{
    /// <summary>Human-readable name used for logging and observability.</summary>
    string Name { get; }

    /// <summary>Declares what this skill does (shown in agent capability lists).</summary>
    string Description { get; }

    /// <summary>Returns true when this skill can handle the request deterministically.</summary>
    bool CanHandle(AgentRequest request);

    /// <summary>Executes the skill: compute domain result, optionally explain via LLM.</summary>
    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken cancellationToken = default);
}
