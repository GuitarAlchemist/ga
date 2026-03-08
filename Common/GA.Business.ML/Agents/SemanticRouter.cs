namespace GA.Business.ML.Agents;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

/// <summary>
/// Routes user requests to the most appropriate agent based on semantic similarity.
/// </summary>
public class SemanticRouter(
    IEnumerable<GuitarAlchemistAgentBase> agents,
    IChatClient? chatClient,
    IEmbeddingGenerator<string, Embedding<float>>? textEmbeddings,
    ILogger<SemanticRouter> logger,
    IRoutingFeedback? routingFeedback = null)
    : IDisposable
{
    private readonly IReadOnlyList<GuitarAlchemistAgentBase> _agents = agents.ToList() is { Count: > 0 } list
        ? list
        : throw new ArgumentException("At least one agent is required", nameof(agents));

    private readonly ILogger<SemanticRouter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Cached embeddings for agent descriptions
    private readonly Dictionary<string, float[]> _agentEmbeddings = [];
    private volatile bool _embeddingsInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Gets the available agents.
    /// </summary>
    public IReadOnlyList<GuitarAlchemistAgentBase> Agents => _agents;

    /// <summary>
    /// Routes a user request to the most appropriate agent.
    /// </summary>
    /// <param name="query">The user's query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The best matching agent.</returns>
    public async Task<RoutingResult> RouteAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        _logger.LogDebug("Routing query: {Query}", query[..Math.Min(100, query.Length)]);

        using var routeActivity = ChatbotActivitySource.StartActivity(ChatbotActivitySource.RoutingRoute, query);

        // 1. Try semantic routing if embeddings are available
        RoutingResult? semanticResult = null;
        if (textEmbeddings != null)
        {
            await EnsureEmbeddingsInitializedAsync(cancellationToken);
            semanticResult = await SemanticRouteAsync(query, cancellationToken);

            // If confidence is high, we can trust it
            if (semanticResult.Confidence > 0.85f)
            {
                routeActivity?.SetTag(ChatbotActivitySource.TagRoutingMethod, semanticResult.RoutingMethod);
                routeActivity?.SetTag(ChatbotActivitySource.TagRoutingConfidence, semanticResult.Confidence);
                routeActivity?.SetTag(ChatbotActivitySource.TagAgentId, semanticResult.SelectedAgent.AgentId);
                return semanticResult;
            }
        }

        // 2. Use LLM routing for refinement if available (especially for low semantic confidence)
        if (chatClient != null)
        {
            var llmResult = await LlmRouteAsync(query, semanticResult, cancellationToken);
            if (llmResult != null && llmResult.Confidence > 0.6f)
            {
                routeActivity?.SetTag(ChatbotActivitySource.TagRoutingMethod, llmResult.RoutingMethod);
                routeActivity?.SetTag(ChatbotActivitySource.TagRoutingConfidence, llmResult.Confidence);
                routeActivity?.SetTag(ChatbotActivitySource.TagAgentId, llmResult.SelectedAgent.AgentId);
                return llmResult;
            }
        }

        // 3. Fallback to semantic or keyword
        var finalResult = semanticResult ?? KeywordRoute(query);
        routeActivity?.SetTag(ChatbotActivitySource.TagRoutingMethod, finalResult.RoutingMethod);
        routeActivity?.SetTag(ChatbotActivitySource.TagRoutingConfidence, finalResult.Confidence);
        routeActivity?.SetTag(ChatbotActivitySource.TagAgentId, finalResult.SelectedAgent.AgentId);
        return finalResult;
    }

    private async Task<RoutingResult?> LlmRouteAsync(
        string query,
        RoutingResult? semanticContext,
        CancellationToken cancellationToken)
    {
        if (chatClient == null) return null;

        var agentDescriptions = string.Join("\n", _agents.Select(a => $"- {a.AgentId}: {a.Description}"));
        var context = semanticContext != null
            ? $"Embeddings suggest: {semanticContext.SelectedAgent.AgentId} ({semanticContext.Confidence:P0})"
            : "No embedding context available.";

        var prompt = $$"""
            You are a musical intent router. Your job is to select the best specialized agent for the user's query.

            Available Agents:
            {{agentDescriptions}}

            Routing Context:
            {{context}}

            User Query: "{{query}}"

            Respond ONLY with a JSON object:
            {
              "agentId": "the_best_agent_id",
              "confidence": 0.95,
              "reasoning": "Brief explanation"
            }
            """;

        try
        {
            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            var text = response.Messages.Last().Text;

            // Clean up JSON if wrapped
            if (text.Contains("```json")) text = text.Split("```json")[1].Split("```")[0].Trim();
            else if (text.Contains("```")) text = text.Split("```")[1].Split("```")[0].Trim();

            var result = JsonSerializer.Deserialize<LlmRoutingResponse>(text, new JsonSerializerOptions
            {
               PropertyNameCaseInsensitive = true
            });

            if (result != null)
            {
                var agent = _agents.FirstOrDefault(a => a.AgentId == result.AgentId);
                if (agent != null)
                {
                    return new RoutingResult
                    {
                        SelectedAgent = agent,
                        Confidence = Math.Clamp(result.Confidence, 0f, 1f),
                        AllScores = semanticContext?.AllScores ?? new List<(GuitarAlchemistAgentBase, float)>(),
                        RoutingMethod = "llm"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM routing failed, falling back.");
        }

        return null;
    }

    private record LlmRoutingResponse(string AgentId, float Confidence, string Reasoning);

    /// <summary>
    /// Processes a request with the appropriate agent, determined by routing.
    /// </summary>
    public async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var routing = await RouteAsync(request.Query, cancellationToken);

        _logger.LogInformation(
            "Routing to {AgentName} (confidence: {Confidence:P0})",
            routing.SelectedAgent.Name,
            routing.Confidence);

        return await routing.SelectedAgent.ProcessAsync(request, cancellationToken);
    }

    /// <summary>
    /// Aggregates responses from multiple agents for complex queries.
    /// </summary>
    public async Task<AggregatedResponse> AggregateAsync(
        AgentRequest request,
        int maxAgents = 3,
        float minConfidence = 0.3f,
        CancellationToken cancellationToken = default)
    {
        var routing = await RouteAsync(request.Query, cancellationToken);

        // Get top N agents above confidence threshold
        var candidateAgents = routing.AllScores
            .Where(s => s.Score >= minConfidence)
            .OrderByDescending(s => s.Score)
            .Take(maxAgents)
            .Select(s => s.Agent)
            .ToList();

        if (!candidateAgents.Any())
        {
            candidateAgents.Add(routing.SelectedAgent);
        }

        _logger.LogInformation(
            "Aggregating responses from {Count} agents: {Agents}",
            candidateAgents.Count,
            string.Join(", ", candidateAgents.Select(a => a.Name)));

        // Process in parallel
        var tasks = candidateAgents.Select(agent =>
            agent.ProcessAsync(request, cancellationToken));

        var responses = await Task.WhenAll(tasks);

        return new AggregatedResponse
        {
            Responses = responses.OrderByDescending(r => r.Confidence).ToList(),
            TopResponse = responses.OrderByDescending(r => r.Confidence).First(),
            ConsensusConfidence = CalculateConsensus(responses)
        };
    }

    private async Task EnsureEmbeddingsInitializedAsync(CancellationToken cancellationToken)
    {
        // Double-check locking: fast path avoids acquiring the semaphore after initialization
        if (_embeddingsInitialized || textEmbeddings == null) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // Second check inside the lock to prevent duplicate initialization
            if (_embeddingsInitialized) return;

            _logger.LogDebug("Initializing agent embeddings...");

            using var initActivity = ChatbotActivitySource.Source.StartActivity(ChatbotActivitySource.EmbeddingInit);
            initActivity?.SetTag(ChatbotActivitySource.TagAgentName, string.Join(",", _agents.Select(a => a.AgentId)));

            var sw = Stopwatch.StartNew();
            var descriptions = _agents.Select(a => $"{a.Name}: {a.Description}. Capabilities: {string.Join(", ", a.Capabilities)}").ToArray();
            var embeddings = await textEmbeddings.GenerateAsync(descriptions, cancellationToken: cancellationToken);

            for (var i = 0; i < _agents.Count; i++)
            {
                _agentEmbeddings[_agents[i].AgentId] = embeddings[i].Vector.ToArray();
            }

            _embeddingsInitialized = true;
            sw.Stop();
            initActivity?.SetTag(ChatbotActivitySource.TagEmbeddingMs, sw.ElapsedMilliseconds);
            _logger.LogInformation("Initialized embeddings for {Count} agents in {Ms}ms", _agents.Count, sw.ElapsedMilliseconds);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<RoutingResult> SemanticRouteAsync(string query, CancellationToken cancellationToken)
    {
        using var activity = ChatbotActivitySource.Source.StartActivity(ChatbotActivitySource.RoutingSemantic);

        var sw = Stopwatch.StartNew();
        var queryEmbedding = await textEmbeddings!.GenerateAsync([query], cancellationToken: cancellationToken);
        var queryVector = queryEmbedding[0].Vector.ToArray();
        activity?.SetTag(ChatbotActivitySource.TagEmbeddingMs, sw.ElapsedMilliseconds);

        var scores = new List<(GuitarAlchemistAgentBase Agent, float Score)>();

        foreach (var agent in _agents)
        {
            if (_agentEmbeddings.TryGetValue(agent.AgentId, out var agentVector))
            {
                var similarity = CosineSimilarity(queryVector, agentVector);
                scores.Add((agent, (float)similarity));
            }
        }

        // Apply learned routing bias from feedback corrections
        if (routingFeedback != null)
        {
            scores = [.. scores.Select(s => (s.Agent, Score: Math.Clamp(s.Score + routingFeedback.GetBias(s.Agent.AgentId), 0f, 1f)))];
        }

        var orderedScores = scores.OrderByDescending(s => s.Score).ToList();
        var best = orderedScores.First();

        var scoresText = string.Join(", ", orderedScores.Select(s => $"{s.Agent.AgentId}={s.Score:F3}"));
        _logger.LogDebug("Semantic routing scores: {Scores}", scoresText);
        activity?.SetTag(ChatbotActivitySource.TagRoutingScores, scoresText);
        activity?.SetTag(ChatbotActivitySource.TagAgentId, best.Agent.AgentId);
        activity?.SetTag(ChatbotActivitySource.TagRoutingConfidence, best.Score);

        return new RoutingResult
        {
            SelectedAgent = best.Agent,
            Confidence = best.Score,
            AllScores = orderedScores,
            RoutingMethod = "semantic"
        };
    }

    private RoutingResult KeywordRoute(string query)
    {
        using var activity = ChatbotActivitySource.Source.StartActivity(ChatbotActivitySource.RoutingKeyword);
        var keywords = new Dictionary<string, string[]>
        {
            [AgentIds.Tab] = ["tab", "tablature", "fret", "string", "ascii", "parse", "e|", "a|", "d|"],
            [AgentIds.Theory] = ["chord", "scale", "key", "mode", "interval", "pitch", "harmonic", "function", "cadence", "theory"],
            [AgentIds.Technique] = ["finger", "position", "play", "technique", "stretch", "barre", "slide", "bend"],
            [AgentIds.Composer] = ["compose", "create", "generate", "reharmonize", "variation", "arrangement"],
            [AgentIds.Critic] = ["evaluate", "critique", "review", "improve", "suggest", "better"]
        };

        var lowerQuery = query.ToLowerInvariant();
        var scores = new List<(GuitarAlchemistAgentBase Agent, float Score)>();

        foreach (var agent in _agents)
        {
            if (keywords.TryGetValue(agent.AgentId, out var agentKeywords))
            {
                var matchCount = agentKeywords.Count(k => lowerQuery.Contains(k));
                var score = (float)matchCount / agentKeywords.Length;
                scores.Add((agent, score));
            }
            else
            {
                scores.Add((agent, 0.1f)); // Default low score
            }
        }

        // Apply learned routing bias from feedback corrections
        if (routingFeedback != null)
        {
            scores = [.. scores.Select(s => (s.Agent, Score: Math.Clamp(s.Score + routingFeedback.GetBias(s.Agent.AgentId), 0f, 1f)))];
        }

        var orderedScores = scores.OrderByDescending(s => s.Score).ToList();
        var best = orderedScores.First();

        // Ensure minimum confidence
        var confidence = Math.Max(best.Score, 0.3f);

        var scoresText = string.Join(", ", orderedScores.Select(s => $"{s.Agent.AgentId}={s.Score:F3}"));
        _logger.LogDebug("Keyword routing scores: {Scores}", scoresText);
        activity?.SetTag(ChatbotActivitySource.TagRoutingScores, scoresText);
        activity?.SetTag(ChatbotActivitySource.TagAgentId, best.Agent.AgentId);
        activity?.SetTag(ChatbotActivitySource.TagRoutingConfidence, confidence);

        return new RoutingResult
        {
            SelectedAgent = best.Agent,
            Confidence = confidence,
            AllScores = orderedScores,
            RoutingMethod = "keyword"
        };
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private static float CalculateConsensus(IReadOnlyList<AgentResponse> responses)
    {
        if (!responses.Any()) return 0f;
        if (responses.Count == 1) return responses[0].Confidence;

        // Simple consensus: average of top confidences, weighted by agreement
        var avgConfidence = responses.Average(r => r.Confidence);
        var varianceMultiplier = 1f - (float)responses.Select(r => r.Confidence).StandardDeviation() / 0.5f;

        return Math.Clamp(avgConfidence * Math.Max(varianceMultiplier, 0.5f), 0f, 1f);
    }

    /// <summary>
    /// Routes to the top-2 agents, runs them in parallel, and — when they disagree —
    /// invokes the <see cref="CriticAgent"/> to adjudicate the winner.
    /// </summary>
    /// <remarks>
    /// Inspired by TARS "multi-agent debate" pattern: parallel drafts expose uncertainty;
    /// a critic arbitrates rather than silently picking the first answer.
    /// Falls back to highest-confidence answer when no CriticAgent is registered
    /// or when the agents agree (consensus ≥ <paramref name="debateThreshold"/>).
    /// </remarks>
    public async Task<DebateResult> DebateAsync(
        AgentRequest request,
        float debateThreshold = 0.5f,
        CancellationToken cancellationToken = default)
    {
        var routing = await RouteAsync(request.Query, cancellationToken);

        // Pick top-2 agents; fall back to the single winner if only one exists
        var topTwo = routing.AllScores
            .OrderByDescending(s => s.Score)
            .Take(2)
            .Select(s => s.Agent)
            .ToList();

        if (topTwo.Count < 2)
        {
            var solo = await topTwo[0].ProcessAsync(request, cancellationToken);
            return new DebateResult
            {
                Winner = solo,
                AllResponses = [solo],
                ConsensusConfidence = solo.Confidence,
                WasDebated = false,
                AdjudicationReason = "Only one candidate agent."
            };
        }

        // Run top-2 in parallel
        var responseTasks = topTwo.Select(a => a.ProcessAsync(request, cancellationToken));
        var responses = await Task.WhenAll(responseTasks);

        var consensus = CalculateConsensus(responses);

        if (consensus >= debateThreshold)
        {
            // Agents broadly agree — take the higher-confidence answer
            var winner = responses.MaxBy(r => r.Confidence)!;
            _logger.LogInformation(
                "Debate: consensus {Consensus:P0} ≥ threshold — winner {AgentId}",
                consensus, winner.AgentId);

            return new DebateResult
            {
                Winner = winner,
                AllResponses = responses,
                ConsensusConfidence = consensus,
                WasDebated = false,
                AdjudicationReason = $"Agents agreed (consensus {consensus:P0})."
            };
        }

        // Agents disagree — invoke CriticAgent to adjudicate
        var critic = _agents.FirstOrDefault(a => a.AgentId == AgentIds.Critic);
        if (critic == null)
        {
            var fallback = responses.MaxBy(r => r.Confidence)!;
            _logger.LogWarning(
                "Debate: low consensus {Consensus:P0} but no CriticAgent registered — using highest confidence",
                consensus);

            return new DebateResult
            {
                Winner = fallback,
                AllResponses = responses,
                ConsensusConfidence = consensus,
                WasDebated = false,
                AdjudicationReason = "No CriticAgent available for adjudication."
            };
        }

        _logger.LogInformation(
            "Debate: low consensus {Consensus:P0} — invoking CriticAgent to adjudicate",
            consensus);

        var debateSummary = string.Join("\n\n", responses.Select((r, i) =>
            $"=== Agent {i + 1}: {r.AgentId} (confidence {r.Confidence:P0}) ===\n{r.Result}"));

        var adjudicationRequest = request with
        {
            Query = $"""
                Two agents gave different answers to this question. Pick the more accurate one and explain why.

                ORIGINAL QUESTION: {request.Query}

                {debateSummary}
                """,
            Context = "You are adjudicating a debate between two music AI agents."
        };

        var adjudication = await critic.ProcessAsync(adjudicationRequest, cancellationToken);

        // Determine which original agent the critic preferred (heuristic: name match in result)
        var preferredResponse = responses.FirstOrDefault(r =>
            adjudication.Result.Contains(r.AgentId, StringComparison.OrdinalIgnoreCase))
            ?? responses.MaxBy(r => r.Confidence)!;

        return new DebateResult
        {
            Winner = preferredResponse with { Confidence = Math.Max(preferredResponse.Confidence, adjudication.Confidence) },
            AllResponses = [.. responses, adjudication],
            ConsensusConfidence = consensus,
            WasDebated = true,
            AdjudicationReason = adjudication.Result
        };
    }

    public void Dispose()
    {
        _initLock.Dispose();
        foreach (var agent in _agents)
        {
            agent.Dispose();
        }
    }
}

/// <summary>
/// Result of semantic routing.
/// </summary>
public record RoutingResult
{
    public required GuitarAlchemistAgentBase SelectedAgent { get; init; }
    public required float Confidence { get; init; }
    public required IReadOnlyList<(GuitarAlchemistAgentBase Agent, float Score)> AllScores { get; init; }
    public required string RoutingMethod { get; init; }
}

/// <summary>
/// Aggregated response from multiple agents.
/// </summary>
public record AggregatedResponse
{
    public required IReadOnlyList<AgentResponse> Responses { get; init; }
    public required AgentResponse TopResponse { get; init; }
    public required float ConsensusConfidence { get; init; }
}

/// <summary>
/// Result of a <see cref="SemanticRouter.DebateAsync"/> invocation.
/// </summary>
public record DebateResult
{
    /// <summary>The winning agent response (adjudicated or highest-confidence).</summary>
    public required AgentResponse Winner { get; init; }

    /// <summary>All responses produced during the debate, including any critic adjudication.</summary>
    public required IReadOnlyList<AgentResponse> AllResponses { get; init; }

    /// <summary>Consensus score before adjudication (0 = total disagreement, 1 = full agreement).</summary>
    public required float ConsensusConfidence { get; init; }

    /// <summary>True when the CriticAgent was invoked to break a disagreement.</summary>
    public required bool WasDebated { get; init; }

    /// <summary>Human-readable explanation of how the winner was chosen.</summary>
    public required string AdjudicationReason { get; init; }
}

/// <summary>
/// Extension methods for statistics.
/// </summary>
internal static class EnumerableExtensions
{
    public static double StandardDeviation(this IEnumerable<float> values)
    {
        var list = values.ToList();
        if (list.Count <= 1) return 0;

        var avg = list.Average();
        var sumSquares = list.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSquares / list.Count);
    }
}
