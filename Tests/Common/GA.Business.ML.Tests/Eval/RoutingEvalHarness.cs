namespace GA.Business.ML.Tests.Eval;

using System.Reflection;
using System.Text.Json;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Extensions;
using GA.Business.ML.Search;
using GA.Domain.Services.Atonal.Grothendieck;
using Domain.Services.Fretboard.Voicings.Filtering;
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
    private const float  RouterMinConfidence = 0.65f;

    private static readonly string DataPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "routing-eval-prompts.json");

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
            var chosenId  = match?.Intent.Id;
            var confidence = match?.Confidence ?? 0f;
            var correct   = string.Equals(chosenId, p.ExpectedIntentId, StringComparison.Ordinal);
            results.Add(new PromptResult(p.Id, p.Prompt, p.ExpectedIntentId, chosenId, confidence, correct, p.Tags));
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
        bool Correct,
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

    private sealed record OverallMetrics(int Total, int Correct, int UnmatchedFallthrough, double Accuracy);

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
            new[] { "what notes are in Cmaj7", "spell a chord", "notes of a Dm7", "what is a chord" });
        AddStubIntent(sc, "skill.scaleinfo", "Scale info — notes of a named scale.",
            new[] { "notes of a scale", "notes in the major scale", "what notes are in A minor" });
        AddStubIntent(sc, "skill.modes", "Modes of the major scale — list and discuss.",
            new[] { "modes of the major scale", "list the modes", "what are the seven modes",
                    "what is Lydian mode", "what notes are in G mixolydian" });
        AddStubIntent(sc, "skill.interval", "Interval between two notes / interval naming.",
            new[] { "interval between two notes", "what is a perfect fifth", "interval from C to G" });
        // skill.fretspan EXCLUDED — see remarks above (regex-routed in production).
        AddStubIntent(sc, "skill.chordsubstitution", "Chord substitution suggestions for a given chord.",
            new[] { "chord substitution", "substitute for a chord", "tritone sub", "what can substitute for" });
        AddStubIntent(sc, "skill.beginnerchords", "Beginner chord suggestions.",
            new[] { "easy chords for beginners", "first chords to learn", "beginner guitar chords" });
        // Per PR #162 review F-2 — production's ProgressionMoodSkill is a
        // TRANSFORM skill ("make this progression sound darker/brighter"),
        // not a DESCRIPTIVE skill. Stub now reflects that semantics.
        AddStubIntent(sc, "skill.progressionmood",
            "Darken or brighten a chord progression via parallel-minor swaps, modal interchange, or borrowed chords.",
            new[] { "make this progression sound darker", "how do I make my song brighter",
                    "darken a major progression", "brighten this minor progression" });
        AddStubIntent(sc, "skill.circleoffifths", "Circle of fifths — positions, relationships.",
            new[] { "circle of fifths", "circle of fourths", "fifths circle" });
        AddStubIntent(sc, "skill.practiceroutine", "Practice routine suggestions.",
            new[] { "practice routine", "give me a practice plan", "20 minute practice" });
        AddStubIntent(sc, "skill.genreessentials", "Essential chords / scales for a genre.",
            new[] { "essential chords for blues", "jazz essentials", "country guitar basics" });
        AddStubIntent(sc, "skill.whatcanyoudo", "Meta — what the assistant can do.",
            new[] { "what can you do", "what are your capabilities", "help me", "what do you know" });
        AddStubIntent(sc, "skill.transpose", "Transpose a chord or progression by an interval.",
            new[] { "transpose to a different key", "transpose down a half step", "transpose up a fifth" });
        AddStubIntent(sc, "skill.commontones", "Common tones between two chords.",
            new[] { "common tones between two chords", "what notes do these chords share" });
        AddStubIntent(sc, "skill.diatonicchords", "Diatonic chords in a key.",
            new[] { "diatonic chords in a key", "chords in the key of C", "what chords are in D major" });
        AddStubIntent(sc, "skill.keyidentification", "Identify the key of a chord progression.",
            new[] { "what key is this progression", "identify the key", "key of a progression" });
        AddStubIntent(sc, "skill.progressioncompletion", "Suggest the next chord in a progression.",
            new[] { "what chord comes next", "complete this progression", "suggest a chord to finish" });
        return sc.BuildServiceProvider();
    }

    private static void AddStubIntent(IServiceCollection sc, string id, string description, string[] examples)
    {
        sc.AddSingleton<IIntent>(new StubIntent(id, description, examples));
    }

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
        return new OverallMetrics(
            Total: total,
            Correct: correct,
            UnmatchedFallthrough: unmatched,
            Accuracy: total == 0 ? 0.0 : (double)correct / total);
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
