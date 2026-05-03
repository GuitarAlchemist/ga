namespace GA.Business.ML.Agents.Intents;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Routes a user query to the best matching <see cref="IIntent"/> by cosine
/// similarity between the query embedding and each intent's example-prompt
/// embeddings. Replaces the legacy string-matching dispatch
/// (<c>KeywordAlgebraPromptClassifier</c>, <c>IsAskingForOptimization</c>,
/// per-skill <c>CanHandle</c> regexes).
/// </summary>
/// <remarks>
/// <para><b>Algorithm:</b></para>
/// <list type="number">
///   <item>On first call, embed every intent's <see cref="IIntent.Description"/>
///         + <see cref="IIntent.ExamplePrompts"/> (cached for the process
///         lifetime — typically a one-time ~50ms cost at startup).</item>
///   <item>Embed the incoming query.</item>
///   <item>For each intent, compute the max cosine similarity across its
///         description + examples.</item>
///   <item>Return the highest scorer if its score crosses
///         <see cref="MinConfidence"/>; otherwise return <c>null</c> so the
///         orchestrator can fall through to its LLM-agent path.</item>
/// </list>
/// <para><b>This is a function, not an agent.</b> No LLM round-trip on the
/// routing path. Cosine similarity is closed-form over fixed vectors. Per
/// <c>docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md</c>
/// §"Routing classifiers".</para>
/// </remarks>
public sealed class SemanticIntentRouter(
    IEmbeddingGenerator<string, Embedding<float>>? textEmbeddings,
    ILogger<SemanticIntentRouter> logger)
{
    private const float DefaultMinConfidence = 0.65f;

    // Process-wide cache so intent vectors persist across requests. Keyed by
    // <see cref="IIntent.Id"/>; each entry is [description, example1..exampleN].
    private readonly Dictionary<string, float[][]> _intentEmbeddings = [];
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _embeddingsReady;

    /// <summary>Cosine-similarity threshold above which an intent claims the query.</summary>
    public float MinConfidence { get; init; } = DefaultMinConfidence;

    /// <summary>
    /// Selects the best intent for the query, or <c>null</c> if no intent
    /// scores above <see cref="MinConfidence"/>.
    /// </summary>
    /// <param name="query">User message.</param>
    /// <param name="services">Per-request <see cref="IServiceProvider"/>. The
    /// router is Singleton; intents may be Scoped (e.g. those that take
    /// <c>IChatClient</c>) so we resolve <c>IEnumerable&lt;IIntent&gt;</c> from
    /// the caller's scope rather than capturing it at construction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IntentMatch?> RouteAsync(
        string query,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        if (textEmbeddings is null) return null;

        var intents = services.GetServices<IIntent>().ToList();
        var candidates = intents.Where(i => i.ExamplePrompts.Count > 0).ToList();
        if (candidates.Count == 0) return null;

        await EnsureExamplesEmbeddedAsync(candidates, cancellationToken);

        float[] queryVec;
        try
        {
            var queryEmbedding = await textEmbeddings.GenerateAsync(
                [query], cancellationToken: cancellationToken);
            queryVec = queryEmbedding[0].Vector.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "SemanticIntentRouter: query embedding failed; routing falls through to LLM path");
            return null;
        }

        IIntent? bestIntent = null;
        var bestScore = float.NegativeInfinity;
        string? bestExample = null;

        foreach (var intent in candidates)
        {
            if (!_intentEmbeddings.TryGetValue(intent.Id, out var vectors)) continue;

            // vectors[0] = description, vectors[1..n] = examples (aligned with
            // intent.ExamplePrompts[i-1]).
            for (var i = 0; i < vectors.Length; i++)
            {
                var score = Cosine(queryVec, vectors[i]);
                if (score <= bestScore) continue;

                bestScore = score;
                bestIntent = intent;
                bestExample = i == 0 ? intent.Description : intent.ExamplePrompts[i - 1];
            }
        }

        if (bestIntent is null || bestScore < MinConfidence)
        {
            logger.LogDebug(
                "SemanticIntentRouter: no intent above threshold {Threshold:F2} (best={Best} @ {Score:F3})",
                MinConfidence, bestIntent?.Id ?? "<none>", bestScore);
            return null;
        }

        logger.LogDebug(
            "SemanticIntentRouter: routed to {Intent} via {Example!r} (score {Score:F3})",
            bestIntent.Id, bestExample, bestScore);

        return new IntentMatch(bestIntent, bestScore, bestExample ?? string.Empty);
    }

    private async Task EnsureExamplesEmbeddedAsync(
        IReadOnlyList<IIntent> candidates, CancellationToken ct)
    {
        // Hot path: every intent we know about has already been embedded.
        if (_embeddingsReady && candidates.All(c => _intentEmbeddings.ContainsKey(c.Id)))
            return;

        await _initLock.WaitAsync(ct);
        try
        {
            // Re-check after acquiring the lock; another thread may have populated.
            var missing = candidates.Where(c => !_intentEmbeddings.ContainsKey(c.Id)).ToList();
            if (missing.Count == 0)
            {
                _embeddingsReady = true;
                return;
            }

            foreach (var intent in missing)
            {
                var inputs = new List<string> { intent.Description };
                inputs.AddRange(intent.ExamplePrompts);

                var batch = await textEmbeddings!.GenerateAsync(inputs, cancellationToken: ct);
                _intentEmbeddings[intent.Id] = batch.Select(e => e.Vector.ToArray()).ToArray();
            }

            _embeddingsReady = true;
            logger.LogInformation(
                "SemanticIntentRouter: embedded {New} new intent(s); cache now holds {Total} intent vectors",
                missing.Count,
                _intentEmbeddings.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "SemanticIntentRouter: example embedding failed; router will degrade to fallback");
            // Leave _embeddingsReady = false so a later call can retry.
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;

        float dot = 0f, na = 0f, nb = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na  += a[i] * a[i];
            nb  += b[i] * b[i];
        }

        if (na == 0f || nb == 0f) return 0f;
        return dot / MathF.Sqrt(na * nb);
    }
}

/// <summary>Result of a successful semantic intent match.</summary>
public readonly record struct IntentMatch(
    IIntent Intent,
    float Confidence,
    string MatchedExample);
