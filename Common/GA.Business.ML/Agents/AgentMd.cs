namespace GA.Business.ML.Agents;

/// <summary>
/// Parsed representation of a declarative agent <c>.md</c> file — YAML frontmatter metadata
/// plus the markdown body that serves as the system prompt.
/// Compatible with TARS <c>AgentDefinition</c> frontmatter schema.
/// </summary>
public sealed record AgentMd
{
    /// <summary>Unique agent identifier (e.g., "tab", "theory").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable agent name.</summary>
    public required string Name { get; init; }

    /// <summary>Agent role for routing (maps to <c>AgentIds</c>).</summary>
    public required string Role { get; init; }

    /// <summary>One-line capability description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Declared capabilities for this agent.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Keywords for keyword-based routing fallback.</summary>
    public IReadOnlyList<string> RoutingKeywords { get; init; } = [];

    /// <summary>Whether this agent uses critique (three-pass: draft → critique → refine).</summary>
    public bool UseCritique { get; init; }

    /// <summary>Agent to delegate to for sub-queries (e.g., composer → theory).</summary>
    public string? DelegatesTo { get; init; }

    /// <summary>The markdown body — injected as the system prompt.</summary>
    public required string Body { get; init; }

    /// <summary>Absolute path of the source file, for diagnostics.</summary>
    public required string FilePath { get; init; }
}
