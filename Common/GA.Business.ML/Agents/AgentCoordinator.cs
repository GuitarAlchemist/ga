namespace GA.Business.ML.Agents;

using Microsoft.Extensions.Logging;

/// <summary>
/// Coordinates cross-agent delegation with depth limiting to prevent infinite recursion.
/// </summary>
public sealed class AgentCoordinator : IAgentCoordinator
{
    private const int MaxDelegationDepth = 3;

    private readonly SemanticRouter _router;
    private readonly ILogger<AgentCoordinator> _logger;
    private static readonly AsyncLocal<int> Depth = new();

    public AgentCoordinator(SemanticRouter router, ILogger<AgentCoordinator> logger)
    {
        _router = router;
        _logger = logger;

        // Wire this coordinator into each agent so they can delegate
        foreach (var agent in router.Agents)
            agent.Coordinator = this;
    }

    /// <inheritdoc />
    public async Task<AgentResponse> DelegateAsync(
        string query,
        string? preferredAgentId = null,
        CancellationToken ct = default)
    {
        var currentDepth = Depth.Value;
        if (currentDepth >= MaxDelegationDepth)
        {
            _logger.LogWarning("Delegation depth limit ({Max}) reached, returning CannotHelp", MaxDelegationDepth);
            return AgentResponse.CannotHelp("coordinator", $"Delegation depth limit ({MaxDelegationDepth}) reached.");
        }

        Depth.Value = currentDepth + 1;
        try
        {
            GuitarAlchemistAgentBase agent;
            if (preferredAgentId is not null)
            {
                agent = _router.Agents.FirstOrDefault(a => a.AgentId == preferredAgentId)
                    ?? throw new InvalidOperationException($"Agent '{preferredAgentId}' not found.");
                _logger.LogInformation("Delegating to {AgentId} (direct) at depth {Depth}", preferredAgentId, Depth.Value);
            }
            else
            {
                var routing = await _router.RouteAsync(query, ct);
                agent = routing.SelectedAgent;
                _logger.LogInformation("Delegating to {AgentId} via {Method} at depth {Depth}",
                    agent.AgentId, routing.RoutingMethod, Depth.Value);
            }

            var request = new AgentRequest { Query = query };
            return await agent.ProcessAsync(request, ct);
        }
        finally
        {
            Depth.Value = currentDepth;
        }
    }
}
