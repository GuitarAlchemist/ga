namespace GA.Business.ML.Tests.Eval;

using System.Reflection;
using System.Text.Json;
using Domain.Services.Fretboard.Voicings.Filtering;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Extensions;
using GA.Business.ML.Search;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Evaluation harness for <see cref="SemanticIntentRouter"/>. Runs a labeled
/// prompt set through the router with REAL embeddings (currently via Ollama
/// at localhost:11434) and reports per-intent precision/recall/F1 plus an
/// overall accuracy score. Writes a baseline JSON to
/// <c>state/quality/routing-eval-&lt;date&gt;.json</c> so successive runs can
/// detect regressions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> Phase 3 task #81 (router-quality). Without an
/// evaluation harness we can't measure whether changes to threshold,
/// example prompts, or hint provider rules actually improve routing
/// accuracy. This is the foundation — future tuning iterations compare
/// against the baseline emitted here.
/// </para>
/// <para>
/// <b>Why [Explicit]:</b> running this requires a live Ollama instance
/// with a chat-embedding model. Normal CI doesn't have one wired, so the
/// test is excluded from default runs. Trigger manually with:
/// <code>dotnet test --filter "FullyQualifiedName~RoutingEvalHarness"</code>
/// </para>
/// <para>
/// <b>Future extension:</b> when CI gains an embedded vector cache (or a
/// stub embedder that proxies recorded responses), this can become a
/// regression gate — fail PRs that drop F1 below the previous baseline.
/// </para>
/// </remarks>
[TestFixture]
public class RoutingEvalHarness
{
    // Env-var overrides per PR #162 review F-7 — operators can point at
    // a different embedder (e.g. Docker Model Runner, hosted Ollama)
    // without recompiling the harness.
    private static readonly string EmbeddingEndpoint =
        Environment.GetEnvironmentVariable("GA_EMBED_ENDPOINT") ?? "http://localhost:11434";
    private static readonly string EmbeddingModel =
        Environment.GetEnvironmentVariable("GA_EMBED_MODEL") ?? "nomic-embed-text";
    // The SAME threshold production routes with. Defaults to the production const
    // (not a hardcoded literal) — this was 0.65f while production sat at 0.55f
    // (dropped 2026-05-13), so the last baseline measured a threshold prod never
    // used. Sourcing the const makes that drift impossible by default;
    // RouterThreshold_DefaultMatchesProductionDefault guards it.
    //
    // GA_ROUTER_MIN_CONFIDENCE overrides it because the threshold is embedder-
    // SPECIFIC (plan #420 Phase 2): a stronger embedder scores higher, so the
    // bge-large baseline must be measured at its recalibrated threshold (~0.64),
    // mirroring AI:Routing:MinConfidence in production appsettings. The ratchet
    // sets GA_EMBED_MODEL + GA_ROUTER_MIN_CONFIDENCE together so the gate measures
    // the embedder/threshold pair production actually deploys.
    private static readonly float RouterMinConfidence =
        ResolveRouterMinConfidence(Environment.GetEnvironmentVariable("GA_ROUTER_MIN_CONFIDENCE"));

    /// <summary>
    /// Parses an explicit threshold override, falling back to the production
    /// const for null/blank/garbage/out-of-range input. Pure + testable so the
    /// drift guard can assert the default path without touching process env.
    /// </summary>
    internal static float ResolveRouterMinConfidence(string? raw) =>
        float.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v is > 0f and <= 1f
            ? v
            : SemanticIntentRouter.DefaultMinConfidence;

    // Sentinel expectedIntentId for out-of-scope prompts: the router SHOULD
    // decline these (return null) so the caller can refuse a non-music query
    // instead of forcing it into the nearest skill. OOS-decline rate is the
    // metric the report's two-step-OOS-gate recommendation needs a baseline for.
    private const string OosSentinel = "__none__";

    // Override the labeled prompt set via GA_EVAL_DATA_PATH (absolute path) so the
    // harness can be run against an independent held-out set (e.g. the Hermes
    // Spike-A heldout-test) without clobbering the checked-in corpus. Mirrors the
    // GA_EMBED_* / GA_EVAL_USE_STUB_REGISTRY override pattern.
    private static readonly string DataPath =
        Environment.GetEnvironmentVariable("GA_EVAL_DATA_PATH")
        ?? Path.Combine(AppContext.BaseDirectory, "Data", "routing-eval-prompts.json");

    /// <summary>
    /// Resolves &lt;repo&gt;/state/quality by walking up from the test bin dir
    /// until we find a .git directory or AllProjects.slnx — robust to
    /// changes in test-project depth or build-config nesting.
    /// </summary>
    private static string ResolveQualityDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (Directory.Exists(Path.Combine(d.FullName, ".git")) ||
                File.Exists(Path.Combine(d.FullName, "AllProjects.slnx")))
            {
                return Path.Combine(d.FullName, "state", "quality");
            }
            d = d.Parent;
        }
        // Fallback: 6-up legacy behaviour, but warn loudly.
        TestContext.WriteLine("WARN: repo root marker not found via walk; falling back to 6-level relative path.");
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "state", "quality"));
    }

    private static readonly string OutputDir = ResolveQualityDir();

    /// <summary>
    /// v0.2 smoke test (not [Explicit]) — pins that the reflection-load
    /// registry constructs every production <c>IOrchestratorSkill</c> and
    /// that the resulting <see cref="IIntent"/> set has ID values matching
    /// the <c>skill.{name}</c> convention. Catches breakage when (a) a new
    /// skill ctor adds a dependency we haven't stubbed, or (b) the
    /// reflection scan stops finding skills (namespace move, build issue).
    /// Runs without Ollama — fast, deterministic, in CI.
    /// </summary>
    [Test]
    [Category("Fast")]
    public void ProductionLikeRegistry_LoadsAllSkillIntents()
    {
        var sp = BuildProductionLikeIntentRegistry();
        var intents = sp.GetServices<IIntent>().ToList();

        Assert.That(intents.Count, Is.GreaterThanOrEqualTo(15),
            "Expected ≥15 IOrchestratorSkill-derived intents from GA.Business.ML " +
            $"(PR #160 / 2026-05-06 graduation count). Found {intents.Count}.");

        // FretSpanSkill intentionally has zero ExamplePrompts — it's
        // dispatched by ProductionOrchestrator's CanHandle regex, not
        // semantic routing. So "examples==0" is NOT malformed; "empty Id"
        // or "missing skill. prefix" is.
        var malformed = intents
            .Where(i => string.IsNullOrWhiteSpace(i.Id) ||
                        !i.Id.StartsWith("skill.", StringComparison.Ordinal))
            .Select(i => $"id='{i.Id}'")
            .ToList();

        Assert.That(malformed, Is.Empty,
            "Reflection-loaded intents have malformed shape (empty Id or missing " +
            $"`skill.` prefix):\n  {string.Join("\n  ", malformed)}");

        // Separate (informational) count of intents that have zero example
        // prompts — these intents won't participate in semantic routing.
        // Logged so changes to the production set are visible in test output.
        var withoutExamples = intents.Where(i => i.ExamplePrompts.Count == 0).Select(i => i.Id).ToList();
        TestContext.WriteLine(
            $"Intents without ExamplePrompts (regex-routed in production): " +
            $"{(withoutExamples.Count == 0 ? "(none)" : string.Join(", ", withoutExamples))}");

        // Spot-check: a few known skill IDs must be present. If these miss,
        // the reflection scan probably picked up the wrong assembly.
        var ids = intents.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in new[] { "skill.chordinfo", "skill.modes", "skill.commontones" })
        {
            Assert.That(ids, Does.Contain(expected),
                $"Expected production intent '{expected}' missing from reflection-loaded registry. " +
                "Registry IDs: " + string.Join(", ", ids.OrderBy(x => x)));
        }
    }

    /// <summary>
    /// Guards the threshold-drift bug: absent an explicit override, the harness
    /// MUST measure the same confidence threshold production routes with. Asserts
    /// the resolver's DEFAULT path (null/blank/garbage → the const) rather than the
    /// env-driven field, so it stays green when the ratchet sets an explicit
    /// GA_ROUTER_MIN_CONFIDENCE for a recalibrated embedder. Fails loudly the moment
    /// someone re-hardcodes a literal default — exactly how the harness drifted to
    /// 0.65 while prod sat at 0.55. Fast, no Ollama, runs in CI.
    /// </summary>
    [Test]
    [Category("Fast")]
    public void RouterThreshold_DefaultMatchesProductionDefault() => Assert.Multiple(() =>
                                                                          {
                                                                              Assert.That(ResolveRouterMinConfidence(null), Is.EqualTo(SemanticIntentRouter.DefaultMinConfidence),
                                                                                  "no override → harness must measure production's default threshold.");
                                                                              Assert.That(ResolveRouterMinConfidence(""), Is.EqualTo(SemanticIntentRouter.DefaultMinConfidence),
                                                                                  "blank override → production default.");
                                                                              Assert.That(ResolveRouterMinConfidence("not-a-number"), Is.EqualTo(SemanticIntentRouter.DefaultMinConfidence),
                                                                                  "garbage override must fall back to the const, never silently route at 0.");
                                                                              Assert.That(ResolveRouterMinConfidence("0.64"), Is.EqualTo(0.64f).Within(1e-6f),
                                                                                  "a valid override is honoured (the bge-large recalibration path).");
                                                                          });

    [Test]
    [Explicit("Requires live Ollama embedding endpoint. Run manually for baselines.")]
    public async Task RunBaseline_EmitReport()
    {
        var labeledPrompts = LoadLabeledPrompts();
        Assert.That(labeledPrompts.Prompts, Is.Not.Empty,
            $"Eval prompt set is empty at {DataPath} — cannot establish a baseline.");

        // v0.2 (task #105) — reflect-load real IOrchestratorSkill implementations
        // from the GA.Business.ML assembly and wrap them via SkillIntentAdapter,
        // so the harness measures discrimination against the PRODUCTION example
        // prompts rather than v0.1's hand-typed reconstruction. Set
        // GA_EVAL_USE_STUB_REGISTRY=1 to opt back into the v0.1 stub registry
        // for harness-validation runs (useful when comparing reflection-load
        // metrics to the hand-typed baseline).
        var services = string.Equals(
            Environment.GetEnvironmentVariable("GA_EVAL_USE_STUB_REGISTRY"),
            "1", StringComparison.Ordinal)
            ? BuildIntentRegistry()
            : BuildProductionLikeIntentRegistry();

        // Real embedder. If unreachable, fail fast with a clear message
        // rather than producing a meaningless all-null baseline.
        IEmbeddingGenerator<string, Embedding<float>> embedder;
        try
        {
            embedder = new OllamaEmbeddingGenerator(new Uri(EmbeddingEndpoint), EmbeddingModel);
            // Warm-call to confirm the model is loaded
            var probe = await embedder.GenerateAsync(["probe"]);
            Assert.That(probe, Is.Not.Null, "Embedder probe returned null.");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive(
                $"Live Ollama at {EmbeddingEndpoint} with model '{EmbeddingModel}' " +
                $"is required for the eval harness — {ex.GetType().Name}: {ex.Message}");
            return;
        }

        var router = new SemanticIntentRouter(
            embedder,
            new DefaultRoutingHintProvider(),
            NullLogger<SemanticIntentRouter>.Instance)
        {
            MinConfidence = RouterMinConfidence,
        };

        var results = new List<PromptResult>();
        foreach (var p in labeledPrompts.Prompts)
        {
            var match = await router.RouteAsync(p.Prompt, services);
            var chosenId   = match?.Intent.Id;
            var confidence = match?.Confidence ?? 0f;
            var isOos      = string.Equals(p.ExpectedIntentId, OosSentinel, StringComparison.Ordinal);
            // OOS ("__none__") prompts are CORRECT iff the router declines
            // (returns null → caller falls through to the LLM / scope-decline
            // path). In-scope prompts are correct iff routed to the expected
            // intent. Without this split, a correctly-declined OOS query scored
            // as a failure (null != "__none__") and the OOS dimension was unmeasurable.
            var correct    = isOos ? chosenId is null
                                   : string.Equals(chosenId, p.ExpectedIntentId, StringComparison.Ordinal);
            // Top1−top2 margin — the escalate-on-ambiguity signal (route to the
            // LLM only when the two best intents are within ~0.05). Null when the
            // router declined (no winner) or only one candidate scored.
            float? margin  = match is { Ranking.Count: >= 2 } m
                ? m.Ranking[0].FinalScore - m.Ranking[1].FinalScore
                : null;
            results.Add(new PromptResult(
                p.Id, p.Prompt, p.ExpectedIntentId, chosenId, confidence, margin, correct, isOos, p.Tags));
        }

        // Aggregate metrics
        var overall = ComputeOverall(results);
        var perIntent = ComputePerIntent(results, labeledPrompts.IntentIdsCovered);

        var report = new
        {
            schemaVersion  = "0.1",
            generatedAt    = DateTime.UtcNow.ToString("o"),
            routerConfig   = new { minConfidence = RouterMinConfidence, embedder = EmbeddingModel },
            totalPrompts   = results.Count,
            overall,
            perIntent,
            prompts        = results,
        };

        // Persist for cohort comparison
        Directory.CreateDirectory(OutputDir);
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var outPath = Path.Combine(OutputDir, $"routing-eval-{stamp}.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        TestContext.WriteLine($"Eval report written: {outPath}");
        TestContext.WriteLine($"Overall: correct={overall.Correct}/{overall.Total} accuracy={overall.Accuracy:P1}");
        TestContext.WriteLine(
            $"  in-scope: {overall.InScopeCorrect}/{overall.InScopeTotal} ({overall.InScopeAccuracy:P1})" +
            $"  ·  OOS-decline: {overall.OosCorrectlyDeclined}/{overall.OosTotal} ({overall.OosDeclineRate:P1})" +
            $"  ·  mean top1−top2 margin: {(overall.MeanInScopeMargin is { } mg ? mg.ToString("F3") : "n/a")}" +
            $"  ·  threshold: {RouterMinConfidence:F2}");
        foreach (var (intentId, m) in perIntent.OrderBy(kv => kv.Key))
        {
            if (m.Status == "no-prompts")
            {
                TestContext.WriteLine($"  {intentId,-32} (no labeled prompts)");
                continue;
            }
            TestContext.WriteLine(
                $"  {intentId,-32} prec={m.Precision:F2} recall={m.Recall:F2} f1={m.F1:F2} (n={m.Support})");
        }

        // Soft assertions — the baseline run shouldn't FAIL, it should
        // RECORD. Future runs can gate on regression against this baseline.
        Assert.That(overall.Total, Is.GreaterThan(0));
    }

    /// <summary>
    /// Dumps the router's EMBEDDING ANCHORS — every routed intent's
    /// <see cref="IIntent.Description"/> + <see cref="IIntent.ExamplePrompts"/>,
    /// embedded with the SAME embedder and SAME normalization
    /// (<c>Trim().ToLowerInvariant()</c>) the production
    /// <see cref="SemanticIntentRouter"/> uses — to
    /// <c>state/quality/routing-diagnostic/routing-anchors-&lt;date&gt;.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists:</b> <see cref="RunBaseline_EmitReport"/> measures
    /// routing <i>accuracy</i> (the symptom). It can't explain <i>why</i> a
    /// prompt misroutes. The router classifies a query by max-cosine against
    /// these anchors, so when two intents' anchor clouds overlap in embedding
    /// space you get misroutes. This dump is the raw material for the IX
    /// DuckDB diagnostic (<c>Scripts/routing-ambiguity-diagnostic.sql</c>):
    /// <c>ix_silhouette</c> scores per-intent separability, a nearest-wrong-
    /// neighbour cross-join names the confusable example-prompt PAIRS, and
    /// <c>ix_pca_project</c> gives a 2-D scatter. The output tells you exactly
    /// which example prompts to add/contrast to separate the clouds — the
    /// semantic, no-keyword-rule lever for fixing the router.
    /// </para>
    /// <para>
    /// <b>Why [Explicit]:</b> needs the live Ollama embedder, same as
    /// <see cref="RunBaseline_EmitReport"/>. Run with:
    /// <code>dotnet test --filter "FullyQualifiedName~DumpRoutingAnchors"</code>
    /// </para>
    /// </remarks>
    [Test]
    [Explicit("Requires live Ollama embedding endpoint. Emits the routing-anchor embedding dump.")]
    public async Task DumpRoutingAnchors_ForAmbiguityDiagnostic()
    {
        var services = BuildProductionLikeIntentRegistry();

        // Only intents with example prompts participate in semantic routing —
        // mirror SemanticIntentRouter's `ExamplePrompts.Count > 0` candidate filter.
        var intents = services.GetServices<IIntent>()
            .Where(i => i.ExamplePrompts.Count > 0)
            .OrderBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
        Assert.That(intents, Is.Not.Empty, "No routed intents with example prompts found.");

        IEmbeddingGenerator<string, Embedding<float>> embedder;
        try
        {
            embedder = new OllamaEmbeddingGenerator(new Uri(EmbeddingEndpoint), EmbeddingModel);
            var probe = await embedder.GenerateAsync(["probe"]);
            Assert.That(probe, Is.Not.Null, "Embedder probe returned null.");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive(
                $"Live Ollama at {EmbeddingEndpoint} with model '{EmbeddingModel}' is required — " +
                $"{ex.GetType().Name}: {ex.Message}");
            return;
        }

        var anchors = new List<AnchorRow>();
        var dimension = 0;
        foreach (var intent in intents)
        {
            // Same anchor set + order the router builds: [description, ...examples],
            // each normalized with Trim().ToLowerInvariant() (NormalizeForEmbedding).
            var texts = new List<(string Kind, string Text)> { ("description", intent.Description) };
            texts.AddRange(intent.ExamplePrompts.Select(p => ("example", p)));

            var inputs = texts.Select(t => NormalizeForEmbeddingLocal(t.Text)).ToList();
            var batch = await embedder.GenerateAsync(inputs);
            for (var i = 0; i < texts.Count; i++)
            {
                var vec = batch[i].Vector.ToArray();
                dimension = vec.Length;
                anchors.Add(new AnchorRow(intent.Id, texts[i].Kind, texts[i].Text, vec));
            }
        }

        var dump = new
        {
            schemaVersion = "0.1",
            generatedAt   = DateTime.UtcNow.ToString("o"),
            embedder      = EmbeddingModel,
            dimension,
            intentCount   = intents.Count,
            anchorCount   = anchors.Count,
            anchors,
        };

        var dir = Path.Combine(OutputDir, "routing-diagnostic");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var outPath = Path.Combine(dir, $"routing-anchors-{stamp}.json");
        // Compact (not indented): the vectors are large; this file is consumed
        // by DuckDB read_json, not read by humans.
        File.WriteAllText(outPath, JsonSerializer.Serialize(dump));

        TestContext.WriteLine($"Routing-anchor dump written: {outPath}");
        TestContext.WriteLine($"  intents={intents.Count} anchors={anchors.Count} dim={dimension}");
        Assert.That(anchors, Has.Count.GreaterThan(intents.Count),
            "Expected more anchors than intents (each intent contributes a description + ≥1 example).");
    }

    // Local copy of SemanticIntentRouter.NormalizeForEmbedding (private there).
    // MUST stay byte-identical so the dumped anchor vectors match the vectors
    // the production router computes for the same text.
    private static string NormalizeForEmbeddingLocal(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Trim().ToLowerInvariant();

    private sealed record AnchorRow(string IntentId, string Kind, string Text, float[] Vector);

    // ─── Data shapes ─────────────────────────────────────────────────────

    private sealed record LabeledPrompt(
        string Id,
        string Prompt,
        string ExpectedIntentId,
        string[]? Tags);

    private sealed record LabeledPromptSet(
        string Version,
        IReadOnlyList<string> IntentIdsCovered,
        IReadOnlyList<LabeledPrompt> Prompts);

    private sealed record PromptResult(
        string Id,
        string Prompt,
        string Expected,
        string? Chosen,
        float Confidence,
        float? Margin,
        bool Correct,
        bool IsOos,
        string[]? Tags);

    private sealed record IntentMetrics(
        int Support,
        int TruePositives,
        int FalsePositives,
        int FalseNegatives,
        double? Precision,
        double? Recall,
        double? F1,
        string Status);   // "measured" | "no-prompts" — disambiguates zero-support from real router failures (PR #162 review F-5)

    private sealed record OverallMetrics(
        int Total,
        int Correct,
        int UnmatchedFallthrough,
        double Accuracy,
        // In-scope routing accuracy (excludes OOS prompts) — the number directly
        // comparable to prior baselines, which had no OOS cases.
        int InScopeTotal,
        int InScopeCorrect,
        double InScopeAccuracy,
        // Out-of-scope decline dimension — of the OOS prompts, how many the router
        // correctly declined (returned null). 1.0 = never forces a non-music query
        // into a skill; lower = the over-firing the report flagged.
        int OosTotal,
        int OosCorrectlyDeclined,
        double OosDeclineRate,
        // Mean top1−top2 margin over in-scope prompts that produced a ranking.
        // The escalate-on-ambiguity tuning signal; null if no in-scope prompt ranked.
        double? MeanInScopeMargin);

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static LabeledPromptSet LoadLabeledPrompts()
    {
        if (!File.Exists(DataPath))
            Assert.Fail($"Labeled prompt set not found at {DataPath} — copy from repo root or rebuild test fixtures.");
        var json = File.ReadAllText(DataPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<LabeledPromptSet>(json, opts);
        Assert.That(parsed, Is.Not.Null, "Labeled prompt set parsed as null.");
        return parsed!;
    }

    /// <summary>
    /// Builds a stub IIntent registry mirroring the production intent IDs
    /// and example prompts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>v0.1 LIMITATION (PR #162 review F-3):</b> this stub is the
    /// harness author's reconstruction of production examples — NOT the
    /// real <c>IOrchestratorSkill.ExamplePrompts</c> values registered by
    /// <c>GaPlugin</c>. For 8 of 17 intents the stub examples diverge
    /// from production, so F1 emitted by this harness measures the stub's
    /// discrimination, not the deployed router's.
    /// </para>
    /// <para>
    /// <b>Until v0.2 lands</b> (reflect-load from production DI), treat
    /// any baseline this harness produces as HARNESS-VALIDATION ONLY,
    /// not a production regression gate.
    /// </para>
    /// <para>
    /// Excluded intents (production routes them via regex, not semantic):
    /// <list type="bullet">
    ///   <item><c>skill.fretspan</c> — FretSpanSkill has no ExamplePrompts;
    ///         dispatched by ProductionOrchestrator's CanHandle regex.</item>
    /// </list>
    /// </para>
    /// </remarks>
    private static IServiceProvider BuildIntentRegistry()
    {
        var sc = new ServiceCollection();
        AddStubIntent(sc, "skill.chordinfo", "Chord info — notes, intervals, quality.",
            ["what notes are in Cmaj7", "spell a chord", "notes of a Dm7", "what is a chord"]);
        AddStubIntent(sc, "skill.scaleinfo", "Scale info — notes of a named scale.",
            ["notes of a scale", "notes in the major scale", "what notes are in A minor"]);
        AddStubIntent(sc, "skill.modes", "Modes of the major scale — list and discuss.",
            [ "modes of the major scale", "list the modes", "what are the seven modes",
                    "what is Lydian mode", "what notes are in G mixolydian" ]);
        AddStubIntent(sc, "skill.interval", "Interval between two notes / interval naming.",
            ["interval between two notes", "what is a perfect fifth", "interval from C to G"]);
        // skill.fretspan EXCLUDED — see remarks above (regex-routed in production).
        AddStubIntent(sc, "skill.chordsubstitution", "Chord substitution suggestions for a given chord.",
            ["chord substitution", "substitute for a chord", "tritone sub", "what can substitute for"]);
        AddStubIntent(sc, "skill.beginnerchords", "Beginner chord suggestions.",
            ["easy chords for beginners", "first chords to learn", "beginner guitar chords"]);
        // Per PR #162 review F-2 — production's ProgressionMoodSkill is a
        // TRANSFORM skill ("make this progression sound darker/brighter"),
        // not a DESCRIPTIVE skill. Stub now reflects that semantics.
        AddStubIntent(sc, "skill.progressionmood",
            "Darken or brighten a chord progression via parallel-minor swaps, modal interchange, or borrowed chords.",
            [ "make this progression sound darker", "how do I make my song brighter",
                    "darken a major progression", "brighten this minor progression" ]);
        AddStubIntent(sc, "skill.circleoffifths", "Circle of fifths — positions, relationships.",
            ["circle of fifths", "circle of fourths", "fifths circle"]);
        AddStubIntent(sc, "skill.practiceroutine", "Practice routine suggestions.",
            ["practice routine", "give me a practice plan", "20 minute practice"]);
        AddStubIntent(sc, "skill.genreessentials", "Essential chords / scales for a genre.",
            ["essential chords for blues", "jazz essentials", "country guitar basics"]);
        AddStubIntent(sc, "skill.whatcanyoudo", "Meta — what the assistant can do.",
            ["what can you do", "what are your capabilities", "help me", "what do you know"]);
        AddStubIntent(sc, "skill.transpose", "Transpose a chord or progression by an interval.",
            ["transpose to a different key", "transpose down a half step", "transpose up a fifth"]);
        AddStubIntent(sc, "skill.commontones", "Common tones between two chords.",
            ["common tones between two chords", "what notes do these chords share"]);
        AddStubIntent(sc, "skill.diatonicchords", "Diatonic chords in a key.",
            ["diatonic chords in a key", "chords in the key of C", "what chords are in D major"]);
        AddStubIntent(sc, "skill.keyidentification", "Identify the key of a chord progression.",
            ["what key is this progression", "identify the key", "key of a progression"]);
        AddStubIntent(sc, "skill.progressioncompletion", "Suggest the next chord in a progression.",
            ["what chord comes next", "complete this progression", "suggest a chord to finish"]);
        return sc.BuildServiceProvider();
    }

    private static void AddStubIntent(IServiceCollection sc, string id, string description, string[] examples) => sc.AddSingleton<IIntent>(new StubIntent(id, description, examples));

    /// <summary>
    /// v0.2 (task #105) — reflect-loads every concrete
    /// <see cref="IOrchestratorSkill"/> implementation in the
    /// <c>GA.Business.ML</c> assembly, instantiates each via DI with minimal
    /// stubs for cross-cutting dependencies (logger, MCP tools, chat-client
    /// factory, Grothendieck service), and wraps each in
    /// <see cref="SkillIntentAdapter"/> so the router enumerates them as
    /// <see cref="IIntent"/>. The harness now measures the router against
    /// production <c>ExamplePrompts</c> values, not the v0.1 hand-typed
    /// reconstruction that diverged for 8 of 17 intents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why reflection (not project ref to Orchestration):</b> the
    /// production <see cref="OrchestratorSkillIntent"/> adapter lives in
    /// <c>GA.Business.Core.Orchestration</c>, which this test project does
    /// not reference. <see cref="SkillIntentAdapter"/> below is a literal
    /// re-implementation matching <c>OrchestratorSkillIntent</c>'s
    /// <see cref="IIntent.Id"/>/<c>Description</c>/<c>ExamplePrompts</c>
    /// projection (execute is a no-op since routing never executes).
    /// </para>
    /// <para>
    /// <b>What this doesn't cover:</b> the four non-skill production
    /// intents (<c>algebra</c>, <c>tab.optimize</c>, <c>tab.analyze</c>,
    /// <c>skill.voicing</c>) registered directly in
    /// <c>ChatbotOrchestrationExtensions</c>. None of them appear in the
    /// v0.1 labeled prompt set, so excluding them doesn't change the
    /// observed F1. If the labeled corpus grows to include them in v0.3,
    /// add a project ref to <c>GA.Business.Core.Orchestration</c> here.
    /// </para>
    /// <para>
    /// <b>Failure mode:</b> if a skill ctor depends on a service we don't
    /// stub, instantiation throws and the test fails Inconclusive with a
    /// list of unresolvable types — actionable signal, not silent skip.
    /// </para>
    /// </remarks>
    private static IServiceProvider BuildProductionLikeIntentRegistry()
    {
        var sc = new ServiceCollection();

        // Logging — every skill takes ILogger<TSelf>.
        sc.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        // Cross-cutting stubs for skill dependencies. Routing never invokes
        // these — they're shape requirements for the ctors only.
        sc.TryAddSingleton<IGrothendieckService, GrothendieckService>();
        sc.TryAddSingleton<IMcpToolsProvider>(_ => new StubMcpToolsProvider());
        sc.TryAddSingleton<IChatClientFactory>(_ => new StubChatClientFactory());
        sc.TryAddSingleton<IChatClient>(_ => new StubChatClient());

        // Voicing-search stack — required by ChordVoicingsSkill (PR #251).
        // Mirrors the OrchestratorTestHarness wiring; the router never
        // exercises these, but the skill ctor demands them.
        sc.AddMemoryCache();
        sc.TryAddSingleton<VoicingIndexingService>();
        sc.TryAddSingleton<IVoicingSearchStrategy, CpuVoicingSearchStrategy>();
        sc.TryAddSingleton<EnhancedVoicingSearchService>();

        // Musical-query extractor stack — required by ChordVoicingsSkill
        // and ImprovisationSkill (PR #253). CompositeMusicalQueryExtractor
        // sits on top of Typed + Llm; both must be registered.
        // MusicalQueryEncoder depends on the four partition vector services
        // — AddMusicalEmbeddings wires them up alongside the rest of the
        // OPTIC-K embedding stack so future additions stay in lockstep with
        // production.
        sc.AddMusicalEmbeddings();
        sc.TryAddSingleton<MusicalQueryEncoder>();
        sc.TryAddSingleton<TypedMusicalQueryExtractor>();
        sc.TryAddSingleton<LlmMusicalQueryExtractor>();
        sc.TryAddSingleton<IMusicalQueryExtractor, CompositeMusicalQueryExtractor>();

        // Reflect-discover every concrete IOrchestratorSkill in the
        // GA.Business.ML assembly. (Skills implemented in host apps —
        // GaApi's VoicingComfortSkill, etc. — aren't covered by this scan
        // by design: they're host-specific and not part of the routed core.)
        var skillTypes = typeof(IOrchestratorSkill).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface &&
                        typeof(IOrchestratorSkill).IsAssignableFrom(t) &&
                        // Skip SkillMdDrivenSkill itself — it requires a parsed
                        // SkillMd record and isn't routed directly; only its
                        // wrappers (SkillMdDrivenWrapperBase descendants) are.
                        t != typeof(SkillMdDrivenSkill))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.That(skillTypes, Is.Not.Empty,
            "Reflection found zero IOrchestratorSkill implementations in GA.Business.ML — " +
            "either the namespace moved or the assembly load failed.");

        foreach (var skillType in skillTypes)
        {
            // Register the skill type itself + an IIntent factory that
            // resolves it through DI and wraps it. Singleton lifetime is
            // fine for routing — the router never calls ExecuteAsync.
            sc.AddSingleton(skillType);
            sc.AddSingleton<IIntent>(sp =>
            {
                var skill = (IOrchestratorSkill)sp.GetRequiredService(skillType);
                return new SkillIntentAdapter(skill);
            });
        }

        var sp = sc.BuildServiceProvider();

        // Eagerly resolve every IIntent so a missing dependency surfaces NOW
        // (with the skill type name) rather than mid-route with an obscure
        // null reference. Failure to resolve = test fails informatively.
        var failures = new List<string>();
        foreach (var skillType in skillTypes)
        {
            try { _ = sp.GetRequiredService(skillType); }
            catch (Exception ex)
            {
                failures.Add($"{skillType.Name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.That(failures, Is.Empty,
            "BuildProductionLikeIntentRegistry could not construct one or more skills. " +
            "Add a stub for the missing dependency or exclude the skill explicitly:\n  " +
            string.Join("\n  ", failures));

        TestContext.WriteLine(
            $"v0.2 production-like registry loaded {skillTypes.Count} skills via reflection.");
        return sp;
    }

    /// <summary>
    /// Local mirror of <c>OrchestratorSkillIntent</c> from
    /// <c>GA.Business.Core.Orchestration</c>. Same Id/Description/Examples
    /// projection so the router sees the exact production shape.
    /// </summary>
    private sealed class SkillIntentAdapter(IOrchestratorSkill skill) : IIntent
    {
        public string Id => $"skill.{skill.Name.ToLowerInvariant().Replace(' ', '-')}";
        public string Description => skill.Description;
        public IReadOnlyList<string> ExamplePrompts => skill.ExamplePrompts;
        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new IntentResult("(no-op for eval)"));
    }

    // ─── Cross-cutting stubs ─────────────────────────────────────────────

    private sealed class StubMcpToolsProvider : IMcpToolsProvider
    {
        public ValueTask<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<AIFunction>>([]);
    }

    private sealed class StubChatClientFactory : IChatClientFactory
    {
        public IChatClient Create(string purpose) => new StubChatClient();
    }

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "StubChatClient.GetResponseAsync was invoked — routing must not execute " +
                "ChatClient at routing time. Check the eval harness flow.");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("StubChatClient.GetStreamingResponseAsync invoked unexpectedly.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class StubIntent(string id, string description, IReadOnlyList<string> examples) : IIntent
    {
        public string Id => id;
        public string Description => description;
        public IReadOnlyList<string> ExamplePrompts => examples;
        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new IntentResult($"(stub for {id})"));
    }

    private static OverallMetrics ComputeOverall(IReadOnlyList<PromptResult> results)
    {
        var total = results.Count;
        var correct = results.Count(r => r.Correct);
        var unmatched = results.Count(r => r.Chosen is null);

        var inScope = results.Where(r => !r.IsOos).ToList();
        var oos = results.Where(r => r.IsOos).ToList();
        var inScopeCorrect = inScope.Count(r => r.Correct);
        // For OOS prompts, "Correct" already means "declined" (see eval loop).
        var oosDeclined = oos.Count(r => r.Correct);
        var margins = inScope.Where(r => r.Margin.HasValue).Select(r => (double)r.Margin!.Value).ToList();

        return new OverallMetrics(
            Total: total,
            Correct: correct,
            UnmatchedFallthrough: unmatched,
            Accuracy: total == 0 ? 0.0 : (double)correct / total,
            InScopeTotal: inScope.Count,
            InScopeCorrect: inScopeCorrect,
            InScopeAccuracy: inScope.Count == 0 ? 0.0 : (double)inScopeCorrect / inScope.Count,
            OosTotal: oos.Count,
            OosCorrectlyDeclined: oosDeclined,
            OosDeclineRate: oos.Count == 0 ? 0.0 : (double)oosDeclined / oos.Count,
            MeanInScopeMargin: margins.Count == 0 ? null : margins.Average());
    }

    private static Dictionary<string, IntentMetrics> ComputePerIntent(
        IReadOnlyList<PromptResult> results,
        IReadOnlyList<string> intentIds)
    {
        var report = new Dictionary<string, IntentMetrics>();
        foreach (var intentId in intentIds)
        {
            var tp = results.Count(r => r.Expected == intentId && r.Chosen  == intentId);
            var fp = results.Count(r => r.Expected != intentId && r.Chosen  == intentId);
            var fn = results.Count(r => r.Expected == intentId && r.Chosen  != intentId);
            var support = results.Count(r => r.Expected == intentId);

            // Per PR #162 review F-5: distinguish "this intent has no
            // labeled prompts" from "router can't route to it." Without
            // this, a future reader sees F1=0 and assumes regression.
            if (support == 0)
            {
                report[intentId] = new IntentMetrics(
                    Support: 0, TruePositives: 0, FalsePositives: fp, FalseNegatives: 0,
                    Precision: null, Recall: null, F1: null, Status: "no-prompts");
                continue;
            }

            var precision = (tp + fp) == 0 ? 0.0 : (double)tp / (tp + fp);
            var recall    = (tp + fn) == 0 ? 0.0 : (double)tp / (tp + fn);
            var f1        = (precision + recall) == 0.0 ? 0.0 : 2.0 * precision * recall / (precision + recall);
            report[intentId] = new IntentMetrics(support, tp, fp, fn, precision, recall, f1, "measured");
        }
        return report;
    }
}
