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

    // ── Tool-invocation failure taxonomy ─────────────────────────────────────
    // Codex CLI second-opinion (2026-05-07) recommended a single structured
    // tool.failure_reason tag over many booleans — easier to query once we
    // have multiple failure modes. Values mirror the wire codes ga_dsl_eval
    // already returns in DslEvalResult.Error so dashboards can correlate
    // chatbot failures with closure-side failures by code.

    /// <summary>Tool name that produced this trace point, e.g. <c>"ga_dsl_eval"</c>, <c>"skill-md"</c>.</summary>
    public const string TagToolName = "tool.name";

    /// <summary>Structured failure code from <see cref="FailureReasons"/> — single string enum, NOT free-form.</summary>
    public const string TagToolFailureReason = "tool.failure_reason";

    /// <summary>SKILL.md skill name when failure originates inside a Path B skill.</summary>
    public const string TagSkillName = "skill.name";

    /// <summary>Closure target the LLM tried to invoke via ga_dsl_eval.</summary>
    public const string TagClosureName = "closure.name";

    /// <summary>Exception type name (no .Message — that may leak endpoint/model details).</summary>
    public const string TagExceptionType = "exception.type";

    /// <summary>
    /// Canonical failure-reason values for <see cref="TagToolFailureReason"/>.
    /// Keep this list closed; if a new failure mode appears, add a constant
    /// here rather than scattering string literals across call sites.
    /// </summary>
    public static class FailureReasons
    {
        /// <summary><c>ga_dsl_eval</c> looked up a closure name that isn't in <c>GaClosureRegistry.Global</c>.</summary>
        public const string ClosureNotFound = "closure-not-found";

        /// <summary>Closure exists but isn't <c>Domain</c>-category (Pipeline/Io/Agent are excluded).</summary>
        public const string ClosureNotExposed = "closure-not-exposed";

        /// <summary>Closure schema declares a required arg the caller didn't provide.</summary>
        public const string MissingRequiredArg = "missing-required-arg";

        /// <summary>Caller-supplied JSON couldn't be coerced into the closure's expected arg type.</summary>
        public const string ArgCoerceFailed = "arg-coerce-failed";

        /// <summary>Closure body returned <c>Result.Error</c> at runtime (domain-level failure).</summary>
        public const string ClosureRuntimeError = "closure-runtime-error";

        /// <summary>Closure exceeded its evaluation timeout.</summary>
        public const string ClosureTimeout = "closure-timeout";

        /// <summary>Closure body threw — distinct from <see cref="ClosureRuntimeError"/> which is a typed Error.</summary>
        public const string ClosureException = "closure-exception";

        /// <summary>SkillMdDrivenSkill catch path — anything thrown inside the LLM tool-loop.</summary>
        public const string SkillMdException = "skill-md-exception";

        /// <summary>Model returned only tool_use blocks (no text turn) or empty whitespace.</summary>
        public const string EmptyModelResponse = "empty-model-response";

        /// <summary>SKILL.md was matched and executed but the LLM never invoked <c>ga_dsl_eval</c>.</summary>
        public const string GaDslEvalNotInvoked = "ga-dsl-eval-not-invoked";

        /// <summary>
        /// Orchestrator returned an answer below the configured fallback
        /// confidence threshold AND no deterministic tool indicated a hard
        /// failure, so the fallback decorator routed the user-visible answer
        /// through a direct chat handler. Confidence is clamped to 0 — codex
        /// CLI 2026-05-08: fallback that hides orchestrator failures behind
        /// a plausible LLM guess is exactly the silent-degradation antipattern
        /// P1 #5 closed; the fallback path must surface this reason in trace
        /// + activity tags so the failure isn't laundered.
        /// </summary>
        public const string OrchestratorLowConfidenceFallback = "orchestrator-low-confidence-fallback";
    }

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
