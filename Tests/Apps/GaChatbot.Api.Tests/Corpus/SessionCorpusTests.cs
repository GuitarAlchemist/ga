namespace GaChatbot.Api.Tests.Corpus;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
///     Replays YAML-defined multi-turn conversations (sessions.yaml) against
///     the GaChatbot.Api host and asserts per-turn invariants, carrying the
///     accumulating ConversationHistory between turns exactly as the live
///     /chatbot UI does. This is the multi-turn counterpart to
///     <see cref="PromptCorpusTests"/>: where that gate sees one-shot answers,
///     this one catches conversation bugs — lost context, a follow-up that
///     falls back, a "those chords" the model no longer remembers.
///
///     DESIGN BOUNDARY: every invariant here is MECHANICAL (routing, grounding
///     presence, no-fallback, context retention, banned phrases, latency) and
///     therefore safe for the autonomous improvement loop to optimise against.
///     Fuzzy answer-quality stays human / Demerzel-tribunal-gated and is not
///     encoded in this file.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SessionCorpusTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private SessionCorpusFile? _corpus;

    // Same universal regression markers PromptCorpusTests enforces — a session
    // answer leaking SKILL.md preamble or an unloaded-index message is a bug
    // regardless of which turn it appears on.
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
            TestContext.CurrentContext.TestDirectory, "Corpus", "sessions.yaml");
        if (!File.Exists(yamlPath))
        {
            yamlPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "Corpus", "sessions.yaml");
        }

        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        _corpus = deserializer.Deserialize<SessionCorpusFile>(yaml)
            ?? throw new InvalidOperationException("sessions.yaml deserialized to null");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public void Sessions_LoadAtLeastOne()
    {
        Assert.That(_corpus!.Sessions, Is.Not.Null);
        Assert.That(_corpus.Sessions.Count, Is.GreaterThan(0),
            "sessions.yaml must contain at least one session");
        foreach (var s in _corpus.Sessions)
            Assert.That(s.Turns.Count, Is.GreaterThan(0), $"session '{s.Id}' has no turns");
    }

    /// <summary>
    ///     Replays every session turn-by-turn and aggregates per-turn failures.
    ///     Explicit because it drives the full orchestrator (Ollama + DI) once
    ///     per turn — too slow for inner-loop CI, but the oracle for the
    ///     multi-turn axis of the improvement loop and a release gate.
    /// </summary>
    [Test]
    [Explicit("Slow — replays full multi-turn sessions against the live orchestrator (Ollama + DI). Run before releases and as the session-axis oracle for the improvement loop.")]
    public async Task EverySession_SatisfiesItsInvariants()
    {
        var failures = new List<string>();
        var warnings = new List<string>();
        var turnsRun = 0;
        var turnsPassed = 0;

        foreach (var session in _corpus!.Sessions)
        {
            // ConversationHistory accumulates across the session — this is what
            // makes "those chords" / "that key" resolvable. Each turn appends
            // the user message and the assistant answer.
            var history = new List<ChatTurn>();

            foreach (var (turn, index) in session.Turns.Select((t, i) => (t, i)))
            {
                turnsRun++;
                var label = $"[{session.Persona}/{session.Id}] turn {index + 1}: '{Truncate(turn.User, 60)}'";

                var maxAttempts = 1 + (turn.Retry ?? 1);
                (string? failure, string? warning, string? answer) result = (null, null, null);
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    result = await EvaluateTurnAsync(label, turn, history);
                    if (result.failure is null) break;
                }

                if (result.failure is not null) failures.Add(result.failure);
                if (result.warning is not null) warnings.Add(result.warning);
                if (result.failure is null) turnsPassed++;

                // Append BOTH sides so the next turn sees the full thread, even
                // when this turn failed an invariant — a later turn shouldn't be
                // penalised for missing context the model was never given.
                history.Add(new ChatTurn("user", turn.User));
                history.Add(new ChatTurn("assistant", result.answer ?? ""));
            }
        }

        var passPct = turnsRun == 0 ? 0 : (double)turnsPassed / turnsRun;
        TestContext.Out.WriteLine(
            $"Sessions: {_corpus.Sessions.Count}  ·  turns: {turnsPassed}/{turnsRun} passed  ·  session_pass_pct={passPct:P1}");

        if (warnings.Count > 0)
        {
            TestContext.Out.WriteLine($"Warnings ({warnings.Count}):");
            foreach (var w in warnings) TestContext.Out.WriteLine($"  ! {w}");
        }

        Assert.That(failures, Is.Empty,
            $"Session turns violating invariants ({failures.Count}):\n  - {string.Join("\n  - ", failures)}");
    }

    // ── evaluation ──────────────────────────────────────────────────────────

    private async Task<(string? failure, string? warning, string? answer)> EvaluateTurnAsync(
        string label, TurnEntry turn, List<ChatTurn> history)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _client!.PostAsJsonAsync(
                "/api/chatbot/chat",
                new
                {
                    message = turn.User,
                    conversationHistory = history.Select(h => new { role = h.Role, content = h.Content }).ToList(),
                });
        }
        catch (Exception ex)
        {
            return ($"{label} → exception: {ex.GetType().Name}: {ex.Message}", null, null);
        }
        sw.Stop();

        if (response.StatusCode != HttpStatusCode.OK)
            return ($"{label} → HTTP {(int)response.StatusCode}", null, null);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var answer = body.TryGetProperty("naturalLanguageAnswer", out var a) ? a.GetString() ?? "" : "";
        var agentId = body.TryGetProperty("agentId", out var ag) ? ag.GetString() : null;
        var grounding = body.TryGetProperty("grounding", out var g) && g.ValueKind == JsonValueKind.Object
            && g.TryGetProperty("source", out var gs) ? gs.GetString() : null;

        if (string.IsNullOrWhiteSpace(answer))
            return ($"{label} → empty response", null, answer);

        var minLen = turn.MinLength ?? 50;
        if (answer.Length < minLen)
            return ($"{label} → too short ({answer.Length} < {minLen} chars)", null, answer);

        foreach (var marker in UniversalBannedMarkers)
            if (answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return ($"{label} → leaked universal banned marker: \"{marker}\"", null, answer);

        if (turn.NotContains is not null)
            foreach (var marker in turn.NotContains)
                if (answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return ($"{label} → contained banned phrase: \"{marker}\"", null, answer);

        if (turn.Contains is not null)
            foreach (var must in turn.Contains)
                if (!answer.Contains(must, StringComparison.OrdinalIgnoreCase))
                    return ($"{label} → missing required substring: \"{must}\"", null, answer);

        if (turn.ContainsAny is { Count: > 0 }
            && !turn.ContainsAny.Any(s => answer.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return ($"{label} → none of contains_any matched: [{string.Join(", ", turn.ContainsAny)}]", null, answer);

        // ── Context-retention invariant (the multi-turn-only signal) ─────────
        // `references` are substrings a CORRECT answer must carry because a
        // prior turn established them. A miss means the chatbot dropped the
        // conversational thread — the failure mode a single-turn corpus is
        // structurally blind to.
        if (turn.References is { Count: > 0 })
        {
            var missing = turn.References
                .Where(r => !answer.Contains(r, StringComparison.OrdinalIgnoreCase))
                .ToList();
            // Require the answer to carry at least one referenced anchor; a
            // total miss is a lost-context failure. (Any-of, not all-of, so a
            // legitimate rephrase that keeps one anchor still passes.)
            if (missing.Count == turn.References.Count)
                return ($"{label} → lost conversation context: none of the prior-turn references appeared [{string.Join(", ", turn.References)}]", null, answer);
        }

        if (!string.IsNullOrWhiteSpace(turn.RoutesTo)
            && !string.Equals(agentId, turn.RoutesTo, StringComparison.OrdinalIgnoreCase))
            return ($"{label} → routed to '{agentId}' but expected '{turn.RoutesTo}'", null, answer);

        if (!string.IsNullOrWhiteSpace(turn.ExpectedGrounding)
            && !string.Equals(grounding, turn.ExpectedGrounding, StringComparison.OrdinalIgnoreCase))
            return ($"{label} → grounding '{grounding ?? "<null>"}' but expected '{turn.ExpectedGrounding}'", null, answer);

        // ── trace-shape invariants ──────────────────────────────────────────
        var traceSteps = ExtractTraceSteps(body);

        if (turn.MustNotFallback == true)
        {
            var fallbackStep = traceSteps.FirstOrDefault(s =>
                string.Equals(s.Name, "orchestration.fallback", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Name, "gen_ai.chat.fallback", StringComparison.OrdinalIgnoreCase));
            if (fallbackStep.Name is not null)
                return ($"{label} → trace visited '{fallbackStep.Name}'; must_not_fallback violated", null, answer);
        }

        if (turn.ForbiddenAgentIds is { Count: > 0 })
            foreach (var step in traceSteps)
            {
                if (step.Attributes is null) continue;
                if (!step.Attributes.TryGetValue("agent.id", out var stepAgentId) || stepAgentId is null) continue;
                if (turn.ForbiddenAgentIds.Any(f => string.Equals(stepAgentId, f, StringComparison.OrdinalIgnoreCase)))
                    return ($"{label} → trace step '{step.Name}' visited forbidden agent.id '{stepAgentId}'", null, answer);
            }

        if (turn.MinRoutingConfidence is { } minConf)
        {
            var answerStep = traceSteps.FirstOrDefault(s =>
                string.Equals(s.Name, "orchestration.answer", StringComparison.OrdinalIgnoreCase));
            if (answerStep.Attributes is not null
                && answerStep.Attributes.TryGetValue("routing.confidence", out var confStr)
                && double.TryParse(confStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var conf)
                && conf < minConf)
                return ($"{label} → routing.confidence {conf:F3} below required minimum {minConf:F3}", null, answer);
        }

        string? warning = null;
        if (turn.MaxElapsedMs.HasValue && sw.ElapsedMilliseconds > turn.MaxElapsedMs.Value)
            warning = $"{label} → {sw.ElapsedMilliseconds}ms exceeded soft budget {turn.MaxElapsedMs}ms";

        return (null, warning, answer);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    // ── YAML shape ──────────────────────────────────────────────────────────

    public sealed class SessionCorpusFile
    {
        public List<SessionEntry> Sessions { get; set; } = [];
    }

    public sealed class SessionEntry
    {
        public string Id { get; set; } = "";
        public string? Persona { get; set; }
        public string? Description { get; set; }
        public List<TurnEntry> Turns { get; set; } = [];
    }

    public sealed class TurnEntry
    {
        public string User { get; set; } = "";
        public string? RoutesTo { get; set; }
        public List<string>? Contains { get; set; }
        public List<string>? ContainsAny { get; set; }
        public List<string>? NotContains { get; set; }

        /// <summary>
        /// Context-retention anchors: substrings the answer must carry because
        /// a prior turn established them. The multi-turn-only invariant.
        /// </summary>
        public List<string>? References { get; set; }

        public int? MinLength { get; set; }
        public int? MaxElapsedMs { get; set; }
        public string? ExpectedGrounding { get; set; }
        public bool? MustNotFallback { get; set; }
        public List<string>? ForbiddenAgentIds { get; set; }
        public double? MinRoutingConfidence { get; set; }
        public int? Retry { get; set; }
    }

    private readonly record struct ChatTurn(string Role, string Content);

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
                    attrs[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Null => null,
                        _ => prop.Value.ToString(),
                    };
            }
            steps.Add(new TraceStep(name!, attrs));
        }
        return steps;
    }
}
