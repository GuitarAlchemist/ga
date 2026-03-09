namespace GA.Business.ML.Agents;

using System.Diagnostics;

/// <summary>
/// Shared <see cref="ActivitySource"/> and tag/operation name constants
/// for the GA chatbot pipeline (routing, agents, embeddings, orchestration).
/// </summary>
/// <remarks>
/// Register with OpenTelemetry in service defaults:
/// <code>
/// tracing.AddSource(ChatbotActivitySource.Name);
/// </code>
/// Spans flow: orchestration.answer → routing.route → agent.{id}.process → llm.chat
/// </remarks>
public static class ChatbotActivitySource
{
    /// <summary>ActivitySource name registered with OpenTelemetry.</summary>
    public const string Name = "GA.Chatbot";

    /// <summary>Semantic version surfaced in Jaeger/Zipkin.</summary>
    public const string Version = "1.0.0";

    /// <summary>The shared ActivitySource instance — one per process.</summary>
    public static readonly ActivitySource Source = new(Name, Version);

    // ── Operation names ──────────────────────────────────────────────────────

    /// <summary>Top-level orchestration span wrapping the full answer pipeline.</summary>
    public const string OrchestratorAnswer = "orchestration.answer";

    /// <summary>SemanticRouter.RouteAsync — decides which agent handles the query.</summary>
    public const string RoutingRoute = "routing.route";

    /// <summary>Cosine-similarity routing among agent embeddings.</summary>
    public const string RoutingSemantic = "routing.semantic";

    /// <summary>LLM-based disambiguation when semantic confidence is low.</summary>
    public const string RoutingLlm = "routing.llm";

    /// <summary>Keyword regex fallback routing.</summary>
    public const string RoutingKeyword = "routing.keyword";

    /// <summary>Parallel debate between top-2 agents, adjudicated by CriticAgent.</summary>
    public const string RoutingDebate = "routing.debate";

    /// <summary>One-time agent embedding initialization.</summary>
    public const string EmbeddingInit = "routing.embedding_init";

    /// <summary>Per-agent processing span — name is suffixed with agent ID.</summary>
    public const string AgentProcess = "agent.process";

    /// <summary>IChatClient.CompleteAsync inside an agent.</summary>
    public const string AgentChat = "agent.chat";

    /// <summary>Three-pass critique loop (draft → critique → refine).</summary>
    public const string AgentChatWithCritique = "agent.chat_with_critique";

    // ── Tag names ────────────────────────────────────────────────────────────

    /// <summary>Routing method chosen: semantic | llm | keyword | none.</summary>
    public const string TagRoutingMethod = "routing.method";

    /// <summary>Routing confidence score [0.0–1.0].</summary>
    public const string TagRoutingConfidence = "routing.confidence";

    /// <summary>All scores as JSON — debug visibility into agent ranking.</summary>
    public const string TagRoutingScores = "routing.scores";

    /// <summary>Agent identifier: theory | tab | technique | composer | critic.</summary>
    public const string TagAgentId = "agent.id";

    /// <summary>Display name of the selected or processing agent.</summary>
    public const string TagAgentName = "agent.name";

    /// <summary>The user query (truncated to 200 chars for brevity).</summary>
    public const string TagQuery = "query";

    /// <summary>Length in characters of the query.</summary>
    public const string TagQueryLength = "query.length";

    /// <summary>Ollama / Claude model ID used for LLM calls.</summary>
    public const string TagLlmModel = "llm.model";

    /// <summary>Whether structured JSON parsing succeeded.</summary>
    public const string TagParseSuccess = "parse.success";

    /// <summary>Elapsed milliseconds for embedding generation.</summary>
    public const string TagEmbeddingMs = "embedding.ms";

    // ── Convenience factories ────────────────────────────────────────────────

    /// <summary>
    /// Starts an activity with a truncated query tag attached.
    /// Returns <see langword="null"/> when no listener is active (zero overhead).
    /// </summary>
    public static Activity? StartActivity(string operationName, string? query = null, ActivityKind kind = ActivityKind.Internal)
    {
        var activity = Source.StartActivity(operationName, kind);
        if (activity is not null && query is not null)
        {
            activity.SetTag(TagQuery, query.Length > 200 ? string.Concat(query.AsSpan(0, 200), "…") : query);
            activity.SetTag(TagQueryLength, query.Length);
        }
        return activity;
    }
}
