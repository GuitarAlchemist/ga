namespace GA.Business.ML.Tests.Eval;

using System.Text.Json;
using GA.Business.ML.Agents.Intents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
[Explicit("Requires live Ollama embedding endpoint. Run manually for baselines.")]
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

    [Test]
    public async Task RunBaseline_EmitReport()
    {
        var labeledPrompts = LoadLabeledPrompts();
        Assert.That(labeledPrompts.Prompts, Is.Not.Empty,
            $"Eval prompt set is empty at {DataPath} — cannot establish a baseline.");

        // Build a minimal DI container with the intents the router routes
        // among. We register stub IIntent instances mirroring the production
        // skill IDs and their example prompts, so the harness measures the
        // router's discrimination quality against a fixed intent corpus
        // independent of skill implementation churn.
        var services = BuildIntentRegistry();

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
