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
    IRoutingHintProvider hintProvider,
    ILogger<SemanticIntentRouter> logger)
{
    // Lowered 2026-05-13 from 0.65 → 0.55 after corpus iter showed short-form
    // queries like "What is dorian" failing to clear the threshold even with
    // the +0.06 routing-hint boost for the mode-name pattern. The +0.06 boost
    // tops out around 0.58–0.62 for short queries against domain-backed
    // skills; 0.55 gives the hint provider room to land its win without
    // letting truly-unrelated queries grab an intent.
    // Public so evaluation harnesses pin to the SAME threshold production
    // routes with — a hardcoded copy in RoutingEvalHarness drifted to 0.65
    // after this dropped to 0.55 (2026-05-13), making the baseline measure a
    // threshold prod never used. One source of truth prevents recurrence.
    public const float DefaultMinConfidence = 0.55f;
    private static readonly TimeSpan DefaultEmbeddingTimeout = TimeSpan.FromSeconds(15);

    // Process-wide cache so intent vectors persist across requests. Keyed by
    // <see cref="IIntent.Id"/>; each entry is [description, example1..exampleN].
    private readonly Dictionary<string, float[][]> _intentEmbeddings = [];
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _embeddingsReady;

    // Embedder model id for the query-embedding sink (Contract B). Resolved once
    // from the live generator's metadata so the persisted rows record the ACTUAL
    // model (e.g. bge-large), never a hardcoded guess. Cached: metadata is static.
    private string? _embedderModelId;
    private bool _embedderResolved;

    /// <summary>Cosine-similarity threshold above which an intent claims the query.</summary>
    public float MinConfidence { get; init; } = DefaultMinConfidence;

    /// <summary>True iff embedding infrastructure is wired. Used by the warmup
    /// hosted service to skip when there's no embedder to call.</summary>
    public bool IsAvailable => textEmbeddings is not null;

    /// <summary>
    /// Per-call hard ceiling on the embedding request. Defends against a
    /// wedged backend (e.g. Ollama in a partial-model-load state) — without
    /// it, routing hangs the whole user request. On timeout the router
    /// returns <c>null</c> so the caller falls through to the LLM agent path.
    /// </summary>
    public TimeSpan EmbeddingTimeout { get; init; } = DefaultEmbeddingTimeout;

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

        // Per-query routing latency starts AFTER the one-time example-embedding
        // warmup (that cost belongs to the first request only, not every query).
        var sw = System.Diagnostics.Stopwatch.StartNew();

        float[] queryVec;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(EmbeddingTimeout);

            var queryEmbedding = await textEmbeddings.GenerateAsync(
                [NormalizeForEmbedding(query)], cancellationToken: timeoutCts.Token);
            queryVec = queryEmbedding[0].Vector.ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "SemanticIntentRouter: query embedding timed out after {Timeout}s (backend likely wedged); falling through to LLM path",
                EmbeddingTimeout.TotalSeconds);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "SemanticIntentRouter: query embedding failed; routing falls through to LLM path");
            return null;
        }

        // Score every candidate intent by its single best (description-or-example)
        // cosine similarity to the query. Keeping per-intent results lets us log
        // the full ranking — much easier to debug than a single best-of accumulator.
        var ranking = new List<(IIntent Intent, float Score, string MatchedSource)>(candidates.Count);
        foreach (var intent in candidates)
        {
            if (!_intentEmbeddings.TryGetValue(intent.Id, out var vectors)) continue;

            // vectors[0] = description, vectors[1..n] = examples aligned with
            // intent.ExamplePrompts[i-1].
            var bestI = -1;
            var bestS = float.NegativeInfinity;
            for (var i = 0; i < vectors.Length; i++)
            {
                var score = Cosine(queryVec, vectors[i]);
                if (score <= bestS) continue;
                bestS = score;
                bestI = i;
            }

            if (bestI >= 0)
            {
                var source = bestI == 0 ? "(description)" : intent.ExamplePrompts[bestI - 1];
                ranking.Add((intent, bestS, source));
            }
        }

        if (ranking.Count == 0)
        {
            logger.LogDebug("SemanticIntentRouter: no intent had cached embeddings");
            return null;
        }

        // Apply deterministic routing-hint boosts BEFORE sorting so high-precision
        // surface patterns ("fret span", "what key is", chord-tone phrasings) can
        // break ties between adjacent semantic centroids. The 2026-05-08
        // capability-matrix smoke surfaced six such ties across ChordInfo /
        // ScaleInfo / Modes / FretSpan / Interval / KeyIdentification /
        // ChordSubstitution — adjacent centroids cosine-tied at the third decimal.
        // Codex CLI 2026-05-08 design call: pure regex rules in
        // DefaultRoutingHintProvider, +0.06 boost capped once per intent.
        var hintDeltas = hintProvider.GetDeltas(query);
        var withHints = new List<(IIntent Intent, float Score, float Boost, string MatchedSource)>(ranking.Count);
        foreach (var (intent, score, source) in ranking)
        {
            var boost = hintDeltas.TryGetValue(intent.Id, out var delta) ? delta : 0f;
            // PR #178 review (LOW): clamp to [0, 1] so the reported
            // confidence stays interpretable as a cosine-like number.
            // Without the clamp, transpose-rule hits could exceed 1.0
            // (cosine 0.95 + boost 0.06 = 1.01), breaking any downstream
            // consumer that asserts `0 ≤ confidence ≤ 1`.
            var boosted = Math.Clamp(score + boost, 0f, 1f);
            withHints.Add((intent, boosted, boost, source));
        }

        // Sort highest-score-first. Tie-break: prefer the intent with the SHORTER
        // description vector — heuristic for "more specific intent wins" when
        // scores are within float precision. (Generic descriptions like
        // KeyIdentification's tend to be longer; specific ones like ChordInfo
        // give the same score to a literal example match but have shorter overall
        // text, signalling tighter scope.)
        withHints.Sort((a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0
                ? byScore
                : a.Intent.Description.Length.CompareTo(b.Intent.Description.Length);
        });

        // Always log the top 3 at Information level so routing decisions are
        // auditable without flipping log levels. Cost is one log line per query.
        // Sanitize the query first: strip control chars (defends plain-text log
        // sinks against \n / ANSI injection) and clamp to 80 chars. Boosted lines
        // show "<base>+<boost>=<final>" so codex's "log both base and hinted scores"
        // requirement is satisfied.
        var topK = withHints.Take(3).ToList();
        logger.LogInformation(
            "SemanticIntentRouter: query={Query} top={Top}",
            SanitizeForLog(query),
            string.Join(" | ", topK.Select(r => r.Boost > 0f
                ? $"{r.Intent.Id}={r.Score - r.Boost:F3}+{r.Boost:F3}={r.Score:F3} via {Trim(r.MatchedSource, 40)}"
                : $"{r.Intent.Id}={r.Score:F3} via {Trim(r.MatchedSource, 40)}")));

        // Routing-decision telemetry (append-only JSONL, error-swallowed). One line
        // per scored query so an uncontaminated held-out eval set can be mined from
        // live traffic — the regex hints are tuned against the fixed corpus, so the
        // harness number is training accuracy; real generalization can only be
        // measured on traffic the hints never saw. See RoutingTelemetryLog.
        var telemetryCandidates = topK
            .Select(r => new RoutingTelemetryCandidate(r.Intent.Id, r.Score - r.Boost, r.Boost, r.Score))
            .ToList();
        var margin = topK.Count >= 2 ? (double?)(topK[0].Score - topK[1].Score) : null;

        var top = withHints[0];

        // SHADOW (default OFF; never affects routing): when GA_ROUTER_SHADOW=1 and a
        // learned head is configured, log the head's pick alongside production's on
        // the SAME query embedding — for offline head-vs-prod comparison and
        // real-traffic eval mining (Hermes Spike-A). Fully error-swallowed.
        if (LearnedHeadShadow.Instance is { } shadow)
        {
            var prodChosenForShadow = top.Score >= MinConfidence ? top.Intent.Id : null;
            shadow.LogShadow(query, queryVec, prodChosenForShadow);
        }

        // Query-embedding sink (Contract B for ix-duck's out-of-domain lens): persist
        // the EXACT vector the router scored with — not a re-embed — plus the decision
        // it drove, so the OOD lens can flag queries far from the in-domain reference
        // set. One row per routed query, covering both the routed and declined paths.
        // Error-swallowed + env-gated like the telemetry above; never affects routing.
        var routed = top.Score >= MinConfidence;
        QueryEmbeddingLog.Append(new QueryEmbeddingRecord
        {
            QueryId         = Guid.NewGuid().ToString("n"),
            Timestamp       = DateTime.UtcNow.ToString("o"),
            QueryText       = query,
            Intent          = routed ? top.Intent.Id : null,
            RouteMethod     = routed ? "embedding" : "fallback",
            RouteConfidence = top.Score,
            Embedder        = ResolveEmbedderModelId(),
            Dim             = queryVec.Length,
            Embedding       = queryVec,
        });

        if (top.Score < MinConfidence)
        {
            logger.LogDebug(
                "SemanticIntentRouter: top score {Score:F3} below threshold {Threshold:F2}; falling through",
                top.Score, MinConfidence);
            RoutingTelemetryLog.Append(new RoutingTelemetryRecord
            {
                Timestamp   = DateTime.UtcNow.ToString("o"),
                Query       = query,
                Chosen      = null,
                Confidence  = null,
                Threshold   = MinConfidence,
                FellThrough = true,
                Margin      = margin,
                Candidates  = telemetryCandidates,
                LatencyMs   = sw.Elapsed.TotalMilliseconds,
            });
            return null;
        }

        // Capture the same top-3 we just logged so the orchestrator can
        // emit a "routing.candidates" trace step. Without this the agentic
        // trace shows only the winner and the user has no idea what came
        // in second — surfaced 2026-05-13 when the user shipped a query
        // that should have hit a different skill but the trace was a
        // single black box.
        var routingCandidates = topK
            .Select(r => new RoutingCandidate(
                IntentId:     r.Intent.Id,
                BaseScore:    r.Score - r.Boost,
                Boost:        r.Boost,
                FinalScore:   r.Score,
                MatchedSource: r.MatchedSource))
            .ToList();

        RoutingTelemetryLog.Append(new RoutingTelemetryRecord
        {
            Timestamp   = DateTime.UtcNow.ToString("o"),
            Query       = query,
            Chosen      = top.Intent.Id,
            Confidence  = top.Score,
            Threshold   = MinConfidence,
            FellThrough = false,
            Margin      = margin,
            Candidates  = telemetryCandidates,
            LatencyMs   = sw.Elapsed.TotalMilliseconds,
        });

        return new IntentMatch(top.Intent, top.Score, top.MatchedSource)
        {
            Ranking = routingCandidates,
        };
    }

    // Best-effort embedder model id from the generator metadata (M.E.AI exposes it
    // via GetService<EmbeddingGeneratorMetadata>). Falls back to "unknown" if the
    // provider doesn't surface a model id — never throws into the routing path.
    private string ResolveEmbedderModelId()
    {
        if (_embedderResolved) return _embedderModelId ?? "unknown";
        _embedderResolved = true;
        try
        {
            var meta = textEmbeddings?.GetService(typeof(EmbeddingGeneratorMetadata)) as EmbeddingGeneratorMetadata;
            _embedderModelId = meta?.DefaultModelId;
        }
        catch
        {
            // metadata resolution is best-effort; leave null → "unknown"
        }
        return _embedderModelId ?? "unknown";
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    // Defense against log injection in plain-text sinks: replace any Unicode
    // control character (Cc / Cf / Cs / Co / Cn) with '·' before clamping to 80
    // chars. Without this, a query containing '\n' could forge a fake log line.
    private static string SanitizeForLog(string s)
    {
        var clamped = s.Length > 80 ? s[..80] + "…" : s;
        var buf = new System.Text.StringBuilder(clamped.Length);
        foreach (var c in clamped)
            buf.Append(char.IsControl(c) ? '·' : c);
        return buf.ToString();
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
                // Lowercase BOTH description and examples before embedding to
                // match the case-normalized query embedding (RouteAsync uses
                // query.ToLowerInvariant() before calling GenerateAsync).
                // Without paired normalization, "DIATONIC CHORDS IN G MAJOR"
                // landed far from "diatonic chords in c major" and routed to
                // skill.transpose. Caught 2026-05-13 via corpus case-variants.
                var inputs = new List<string> { NormalizeForEmbedding(intent.Description) };
                inputs.AddRange(intent.ExamplePrompts.Select(NormalizeForEmbedding));

                var batch = await textEmbeddings!.GenerateAsync(inputs, cancellationToken: ct);
                _intentEmbeddings[intent.Id] = [.. batch.Select(e => e.Vector.ToArray())];
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

    // Embedding-side normalization: lowercase + trim only. The embedding
    // model (nomic-embed-text) tokenizes case-sensitively and produces
    // measurably different vectors for "X" vs "x" — enough to flip
    // routing winners on close calls. Applying the same normalization
    // to both stored examples and the runtime query keeps comparisons
    // case-invariant without losing semantic content. Skills still get
    // the ORIGINAL message (case preserved) at execution time.
    private static string NormalizeForEmbedding(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Trim().ToLowerInvariant();

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
    string MatchedExample)
{
    /// <summary>
    /// Top-K (default 3) candidates considered during routing, ordered by
    /// final score (highest first). Includes the selected winner at index 0.
    /// Populated by <see cref="SemanticIntentRouter"/> so the orchestrator
    /// can emit a "routing.candidates" trace step showing what competed and
    /// what didn't fire — surfacing the routing decision instead of hiding
    /// it inside the orchestrator's "orchestration.answer" black-box step.
    /// </summary>
    public IReadOnlyList<RoutingCandidate> Ranking { get; init; } = [];
}

/// <summary>
/// One candidate in a routing trace. The fields let downstream consumers
/// reconstruct exactly what happened: base cosine score, the boost that
/// hint-rules added (if any), the final score that determined ranking, and
/// the example string the candidate centroid matched against.
/// </summary>
public readonly record struct RoutingCandidate(
    string IntentId,
    float BaseScore,
    float Boost,
    float FinalScore,
    string MatchedSource);
