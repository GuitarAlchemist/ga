namespace GA.Business.ML.Agents;

/// <summary>
/// Enables cross-agent delegation: an agent can ask another agent to handle a sub-query.
/// </summary>
public interface IAgentCoordinator
{
    /// <summary>
    /// Delegates a query to another agent via semantic routing (or direct ID lookup).
    /// </summary>
    /// <param name="query">The sub-query to delegate.</param>
    /// <param name="preferredAgentId">If set, routes directly to this agent instead of using the router.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The delegated agent's response.</returns>
    Task<AgentResponse> DelegateAsync(
        string query,
        string? preferredAgentId = null,
        CancellationToken ct = default);
}
