namespace GaChatbot.Api.Tests.Corpus;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
///     Runs a YAML-defined corpus of prompts against the GaChatbot.Api host
///     and asserts per-prompt invariants. This is the safety net under
///     ongoing skill refactors: a failing prompt fails the gate before merge,
///     and the same prompt set is the oracle for the autonomous improvement
///     loop (task #144).
///
///     Why GaChatbot.Api and not GaApi: the GaApi smoke test
///     (ChatbotShowcaseSmokeTests) tests a host that cloudflared no longer
///     routes to. Live demo traffic hits GaChatbot.Api on :5252. This file
///     tests the same code path users actually exercise.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PromptCorpusTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private CorpusFile? _corpus;
    private string? _goldenTracesRoot;

    /// <summary>
    ///     Universally-banned substrings — any answer containing these is a
    ///     regression regardless of the prompt. Per-prompt `not_contains`
    ///     entries add on top.
    /// </summary>
    private static readonly string[] UniversalBannedMarkers =
    [
        "Reproduce the catalog below verbatim",
        "Pure pedagogy",
        "Use when a learner asks",
        "Use when a visitor asks",
        "doesn't need a tool call",
        "returned no matches",
        "not yet implemented",
        "index is not loaded",
    ];

    /// <summary>
    ///     Machine-readable token stamped into a failure message when the backend
    ///     answered in degraded mode. <c>Scripts/run-prompt-corpus.ps1</c> matches
    ///     this to classify the run as an environment failure.
    /// </summary>
    internal const string BackendDegradedToken = "BACKEND_DEGRADED";

    /// <summary>
    ///     Graceful-degradation signatures. When the backend cannot serve — no
    ///     chat model configured (the model-less CI case), readiness probe failing,
    ///     or an unloadable SKILL.md — the chatbot deliberately returns polite
    ///     fallback prose instead of an HTTP error. Those prompts therefore fail as
    ///     ordinary invariant mismatches, and the snapshot writer's transport-error
    ///     heuristic (HTTP 5xx / connection refused / timed out) never fires. The
    ///     result was a month of daily snapshots publishing <c>pass_pct: 7.69</c>
    ///     with <c>degraded: false</c> while the chatbot actually scored 98.08%
    ///     against a real model — graceful degradation defeating the degradation
    ///     detector. Stamping <see cref="BackendDegradedToken"/> lets the snapshot
    ///     writer emit "no signal" instead of a fabricated pass-rate.
    ///     Sources: <c>ReadinessGatedChatApplicationService</c> (readiness refusal)
    ///     and <c>SkillMdDrivenWrapperBase</c>'s catch-block <c>DegradedResponseText</c>
    ///     (emitted only on the exception path, Confidence = 0).
    /// </summary>
    private static readonly string[] BackendDegradedMarkers =
    [
        "The chatbot can't serve a request right now",
        "right now. Please try again.",
    ];

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new GaChatbot.Api.Tests.TestWebApplicationFactory();
        _client = _factory.CreateClient();
        _client.Timeout = TimeSpan.FromMinutes(3);

        var yamlPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Corpus",
            "prompts.yaml");

        if (!File.Exists(yamlPath))
        {
            // Fall back to the source-tree location so the corpus stays
            // editable without rebuilding (the csproj will be updated to
            // CopyToOutputDirectory=PreserveNewest in the same change).
            yamlPath = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..",
                "Corpus",
                "prompts.yaml");
        }

        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        _corpus = deserializer.Deserialize<CorpusFile>(yaml)
            ?? throw new InvalidOperationException("prompts.yaml deserialized to null");

        // Resolve the golden-traces root if it exists. The path is optional —
        // when a signature file is committed for a prompt, the trace-vs-
        // signature check fires; otherwise it skips silently. This is the
        // .NET counterpart to Scripts/compare-trace-to-canonical.ps1 so the
        // same shape contract runs both in CI and in local PowerShell.
        // Candidates cover both the source-tree path (developer test run)
        // and any future test fixture co-location.
        var goldenCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
                "state", "quality", "chatbot-qa", "golden-traces"),
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "..",
                "state", "quality", "chatbot-qa", "golden-traces"),
        };
        _goldenTracesRoot = goldenCandidates
            .Select(p => Path.GetFullPath(p))
            .FirstOrDefault(Directory.Exists);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public void Corpus_LoadsAtLeastOnePrompt()
    {
        Assert.That(_corpus!.Prompts, Is.Not.Null);
        Assert.That(_corpus.Prompts.Count, Is.GreaterThan(0),
            "prompts.yaml must contain at least one prompt entry");
    }

    [Test]
    public void Corpus_EverySkipHasReason()
    {
        var orphans = _corpus!.Prompts
            .Where(p => p.Skip && string.IsNullOrWhiteSpace(p.SkipReason))
            .Select(p => p.Prompt)
            .ToList();
        Assert.That(orphans, Is.Empty,
            $"Skipped prompts without a skip_reason:\n  - {string.Join("\n  - ", orphans)}");
    }

    /// <summary>
    ///     Runs every non-skipped prompt and aggregates failures. Explicit
    ///     because it talks to Ollama and the full orchestrator graph — too
    ///     slow for inner-loop CI but fast enough to gate releases.
    /// </summary>
    /// <remarks>
    ///     Per-prompt retry policy: each entry's `retry: N` field controls
    ///     how many additional attempts the runner makes before declaring a
    ///     prompt failed. Default is 1 retry (so 2 attempts total) for
    ///     prompts whose answer goes through an LLM-shaped formatter where
    ///     wording can legitimately vary run-to-run. Set retry: 0 for
    ///     pure-deterministic skill answers where any variance is a real
    ///     bug. Latency warnings are emitted from the LAST attempt's
    ///     elapsed time only.
    /// </remarks>
    [Test]
    [Explicit("Slow — runs the full corpus against the live orchestrator (Ollama + DI). Run before releases and as the oracle for the Cherny improvement loop.")]
    public async Task EveryPrompt_SatisfiesItsInvariants()
    {
        var failures = new List<string>();
        var warnings = new List<string>();

        foreach (var entry in _corpus!.Prompts)
        {
            if (entry.Skip) continue;
            // Retry policy: default 1 retry (so up to 2 attempts) per prompt
            // to absorb LLM non-determinism. Explicit retry: 0 in the entry
            // disables this for deterministic-skill prompts where wording
            // variance IS a real failure.
            var maxAttempts = 1 + (entry.Retry ?? 1);
            (string? failure, string? warning) result = (null, null);
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                result = await EvaluatePromptAsync(entry);
                if (result.failure is null) break;
            }
            if (result.failure is not null) failures.Add(result.failure);
            if (result.warning is not null) warnings.Add(result.warning);
        }

        if (warnings.Count > 0)
        {
            TestContext.Out.WriteLine($"Warnings ({warnings.Count}):");
            foreach (var w in warnings) TestContext.Out.WriteLine($"  ! {w}");
        }

        Assert.That(failures, Is.Empty,
            $"Prompts violating invariants ({failures.Count}):\n  - {string.Join("\n  - ", failures)}");
    }

    // ── evaluation ──────────────────────────────────────────────────────────

    private async Task<(string? failure, string? warning)> EvaluatePromptAsync(PromptEntry entry)
    {
        var label = $"[{entry.Category ?? "?"}] '{entry.Prompt}'";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _client!.PostAsJsonAsync(
                "/api/chatbot/chat",
                new { message = entry.Prompt });
        }
        catch (Exception ex)
        {
            return ($"{label} → exception: {ex.GetType().Name}: {ex.Message}", null);
        }
        sw.Stop();

        if (response.StatusCode != HttpStatusCode.OK)
            return ($"{label} → HTTP {(int)response.StatusCode}", null);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var answer = body.TryGetProperty("naturalLanguageAnswer", out var a) ? a.GetString() ?? "" : "";
        var agentId = body.TryGetProperty("agentId", out var ag) ? ag.GetString() : null;
        var routingMethod = body.TryGetProperty("routingMethod", out var rm) ? rm.GetString() : null;
        var grounding = body.TryGetProperty("grounding", out var g) && g.ValueKind == JsonValueKind.Object
            && g.TryGetProperty("source", out var gs)
                ? gs.GetString()
                : null;

        if (string.IsNullOrWhiteSpace(answer))
            return ($"{label} → empty response", null);

        // Checked BEFORE the content invariants: a degraded backend answers with
        // polite fallback prose, which would otherwise be scored as an ordinary
        // "missing substring" failure and published as a real pass-rate.
        foreach (var marker in BackendDegradedMarkers)
            if (answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return ($"{label} → {BackendDegradedToken}: backend returned a degraded fallback answer", null);

        var minLen = entry.MinLength ?? 50;
        if (answer.Length < minLen)
            return ($"{label} → too short ({answer.Length} < {minLen} chars)", null);

        foreach (var marker in UniversalBannedMarkers)
            if (answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return ($"{label} → leaked universal banned marker: \"{marker}\"", null);

        if (entry.NotContains is not null)
        {
            foreach (var marker in entry.NotContains)
                if (answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return ($"{label} → contained banned phrase: \"{marker}\"", null);
        }

        if (entry.Contains is not null)
        {
            foreach (var must in entry.Contains)
                if (!answer.Contains(must, StringComparison.OrdinalIgnoreCase))
                    return ($"{label} → missing required substring: \"{must}\"", null);
        }

        if (entry.ContainsAny is not null && entry.ContainsAny.Count > 0)
        {
            var hit = entry.ContainsAny.Any(s => answer.Contains(s, StringComparison.OrdinalIgnoreCase));
            if (!hit)
                return ($"{label} → none of contains_any matched: [{string.Join(", ", entry.ContainsAny)}]", null);
        }

        if (!string.IsNullOrWhiteSpace(entry.RoutesTo)
            && !string.Equals(agentId, entry.RoutesTo, StringComparison.OrdinalIgnoreCase))
            return ($"{label} → routed to '{agentId}' but expected '{entry.RoutesTo}'", null);

        if (!string.IsNullOrWhiteSpace(entry.RoutingMethod)
            && !string.Equals(routingMethod, entry.RoutingMethod, StringComparison.OrdinalIgnoreCase))
            return ($"{label} → routing method '{routingMethod}' but expected '{entry.RoutingMethod}'", null);

        if (!string.IsNullOrWhiteSpace(entry.ExpectedGrounding)
            && !string.Equals(grounding, entry.ExpectedGrounding, StringComparison.OrdinalIgnoreCase))
            return ($"{label} → grounding '{grounding ?? "<null>"}' but expected '{entry.ExpectedGrounding}'", null);

        // ── trace-shape invariants ──────────────────────────────────────────
        // The chatbot response carries a structured trace
        // (response.trace.steps[]). Treating the trace as the test surface —
        // rather than the answer text — catches *trajectory* bugs (wrong
        // skill picked, fallback fired, low-confidence routing) before they
        // turn into text-pattern failures, which is what the GitHub
        // "validating agentic behavior" framework calls out: don't judge
        // outputs when the path itself is the evidence.
        //
        // Three invariants today, all optional and opt-in per prompt:
        //   - must_not_fallback:        no "orchestration.fallback" or
        //                               "gen_ai.chat.fallback" step
        //   - forbidden_agent_ids:      none of these may appear as
        //                               agent.id on any trace step
        //   - min_routing_confidence:   routing.confidence on the answer
        //                               step must be >= this value
        // Each fires only when the corresponding field is set on the
        // PromptEntry, so adding the field is a per-prompt opt-in.

        var traceSteps = ExtractTraceSteps(body);

        if (entry.MustNotFallback == true)
        {
            var fallbackStep = traceSteps.FirstOrDefault(s =>
                string.Equals(s.Name, "orchestration.fallback", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Name, "gen_ai.chat.fallback",   StringComparison.OrdinalIgnoreCase));
            if (fallbackStep.Name is not null)
            {
                var reason = fallbackStep.Attributes is not null
                    && fallbackStep.Attributes.TryGetValue("fallback.reason", out var r) ? r : "<unknown>";
                return ($"{label} → trace visited '{fallbackStep.Name}' (fallback.reason={reason}); must_not_fallback violated", null);
            }
        }

        if (entry.ForbiddenAgentIds is { Count: > 0 })
        {
            foreach (var step in traceSteps)
            {
                if (step.Attributes is null) continue;
                if (!step.Attributes.TryGetValue("agent.id", out var stepAgentId) || stepAgentId is null) continue;
                foreach (var forbidden in entry.ForbiddenAgentIds)
                {
                    if (string.Equals(stepAgentId, forbidden, StringComparison.OrdinalIgnoreCase))
                        return ($"{label} → trace step '{step.Name}' visited forbidden agent.id '{forbidden}'", null);
                }
            }
        }

        if (entry.MinRoutingConfidence is { } minConf)
        {
            var answerStep = traceSteps.FirstOrDefault(s =>
                string.Equals(s.Name, "orchestration.answer", StringComparison.OrdinalIgnoreCase));
            if (answerStep.Attributes is not null
                && answerStep.Attributes.TryGetValue("routing.confidence", out var confStr)
                && double.TryParse(confStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var conf)
                && conf < minConf)
            {
                return ($"{label} → routing.confidence {conf:F3} below required minimum {minConf:F3}", null);
            }
        }

        // ── Canonical signature check ───────────────────────────────────────
        // If a _signature.json file is committed for this prompt's slug
        // under state/quality/chatbot-qa/golden-traces/, assert the live
        // trace's (name, agent.id) per step matches. This is the offline-
        // validation pipeline's CI wire-up — produced by
        // Scripts/extract-trace-dominators.ps1 and committed once vetted.
        // Prompts without a signature file silently skip this check, so
        // the gate is opt-in per prompt by file presence (no YAML field
        // needed).
        if (_goldenTracesRoot is not null)
        {
            var sigPath = CanonicalSignatureChecker.ResolveSignaturePath(_goldenTracesRoot, entry.Prompt);
            if (sigPath is not null)
            {
                var signature = CanonicalSignatureChecker.Load(sigPath);
                var trace = traceSteps.Select(s => (s.Name, s.Attributes)).ToList();
                var sigDiff = CanonicalSignatureChecker.Compare(trace, signature);
                if (sigDiff is not null)
                {
                    return ($"{label} → canonical signature mismatch: {sigDiff}", null);
                }
            }
        }

        // Latency warnings (not failures — production latency varies)
        string? warning = null;
        if (entry.MaxElapsedMs.HasValue && sw.ElapsedMilliseconds > entry.MaxElapsedMs.Value)
            warning = $"{label} → {sw.ElapsedMilliseconds}ms exceeded soft budget {entry.MaxElapsedMs}ms";

        return (null, warning);
    }

    // ── YAML shape (deserialized via UnderscoredNamingConvention) ───────────

    public sealed class CorpusFile
    {
        public List<PromptEntry> Prompts { get; set; } = [];
    }

    public sealed class PromptEntry
    {
        public string Prompt { get; set; } = "";
        public string? Category { get; set; }
        public string? RoutesTo { get; set; }
        public string? RoutingMethod { get; set; }
        public List<string>? Contains { get; set; }
        public List<string>? ContainsAny { get; set; }
        public List<string>? NotContains { get; set; }
        public int? MinLength { get; set; }
        public int? MaxElapsedMs { get; set; }
        public string? ExpectedGrounding { get; set; }
        public bool Skip { get; set; }
        public string? SkipReason { get; set; }

        // Retry count: how many ADDITIONAL attempts the runner makes
        // before declaring a prompt failed. Default is 1 (so 2 attempts
        // total). Set to 0 for deterministic-skill prompts where any
        // wording variance is a real bug.
        public int? Retry { get; set; }

        // ── Trace-shape invariants (GitHub agentic-behavior validation) ─────
        // These check the structured trace returned with every chat
        // response, not the answer text. They catch trajectory bugs
        // before they degrade into text-pattern failures.
        //
        // YAML form (snake_case via UnderscoredNamingConvention):
        //   must_not_fallback: true
        //   forbidden_agent_ids: [skill.relativekey, skill.chordsubstitution]
        //   min_routing_confidence: 0.5
        //
        // Each is opt-in per prompt. Existing prompts without these
        // fields run with the same invariant set as before.

        /// <summary>
        /// If true, the trace must not visit "orchestration.fallback" or
        /// "gen_ai.chat.fallback" steps. Useful for prompts the chatbot
        /// should answer deterministically — fallback firing is itself
        /// the bug.
        /// </summary>
        public bool? MustNotFallback { get; set; }

        /// <summary>
        /// Agent IDs that must not appear on any trace step's `agent.id`
        /// attribute. Useful for prompts the chatbot routes incorrectly
        /// via semantic similarity (e.g., "what is major vs minor"
        /// semantically pulls skill.relativekey).
        /// </summary>
        public List<string>? ForbiddenAgentIds { get; set; }

        /// <summary>
        /// Minimum `routing.confidence` on the orchestration.answer step.
        /// Low-confidence picks are not actively wrong, but if the corpus
        /// asserts a specific routes_to + min_routing_confidence, regressions
        /// where the orchestrator picks the right skill weakly become
        /// visible.
        /// </summary>
        public double? MinRoutingConfidence { get; set; }
    }

    private readonly record struct TraceStep(string Name, IDictionary<string, string?>? Attributes);

    private static List<TraceStep> ExtractTraceSteps(JsonElement body)
    {
        var steps = new List<TraceStep>();
        if (!body.TryGetProperty("trace", out var trace) || trace.ValueKind != JsonValueKind.Object)
            return steps;
        if (!trace.TryGetProperty("steps", out var stepsArr) || stepsArr.ValueKind != JsonValueKind.Array)
            return steps;

        foreach (var step in stepsArr.EnumerateArray())
        {
            var name = step.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrEmpty(name)) continue;

            Dictionary<string, string?>? attrs = null;
            if (step.TryGetProperty("attributes", out var attrsObj) && attrsObj.ValueKind == JsonValueKind.Object)
            {
                attrs = [];
                foreach (var prop in attrsObj.EnumerateObject())
                {
                    attrs[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Null   => null,
                        _                    => prop.Value.ToString(),
                    };
                }
            }
            steps.Add(new TraceStep(name!, attrs));
        }
        return steps;
    }
}
