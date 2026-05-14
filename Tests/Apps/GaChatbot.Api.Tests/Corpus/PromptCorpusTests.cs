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
    }
}
