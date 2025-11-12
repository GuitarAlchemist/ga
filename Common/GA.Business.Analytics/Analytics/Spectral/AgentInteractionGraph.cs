namespace GA.Business.Analytics.Analytics.Spectral;

using JetBrains.Annotations;

/// <summary>
///     Represents an interaction graph between autonomous agents.
/// </summary>
[PublicAPI]
public sealed record AgentInteractionGraph
{
    /// <summary>
    ///     Participating agent nodes.
    /// </summary>
    public required IReadOnlyList<AgentNode> Agents { get; init; }

    /// <summary>
    ///     Weighted edges modelling influence or agreement between agents.
    /// </summary>
    public required IReadOnlyList<AgentInteractionEdge> Edges { get; init; }

    /// <summary>
    ///     Indicates whether edges should be treated as undirected.
    /// </summary>
    public bool IsUndirected { get; init; } = true;

    /// <summary>
    ///     Optional metadata associated with the snapshot.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>
///     Agent node descriptor.
/// </summary>
[PublicAPI]
public sealed record AgentNode
{
    public required string Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public double Weight { get; init; } = 1.0;
    public IDictionary<string, double> Signals { get; init; } = new Dictionary<string, double>();
}

/// <summary>
///     Weighted interaction between agents.
/// </summary>
[PublicAPI]
public sealed record AgentInteractionEdge
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public double Weight { get; init; } = 1.0;
    public IDictionary<string, double> Features { get; init; } = new Dictionary<string, double>();
}
