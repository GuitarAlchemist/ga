namespace GA.Business.ML.Tests.Unit;

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// The Jev routing shadow against a stub HTTP handler — no call ever leaves the
/// process. Pins the request contract, fail-closed validation, the budget, the
/// timeout and that the API key never reaches the log.
/// </summary>
[TestFixture]
public class JevRoutingShadowTests
{
    private const string Key = "ts-secret-key-do-not-leak";
    private string _dir = null!;

    private static readonly IReadOnlyList<IIntent> Intents =
    [
        new StubIntent("chordinfo", "Explains what notes a chord contains."),
        new StubIntent("scaleinfo", "Explains the notes and degrees of a scale."),
    ];

    [SetUp]
    public void SetUp() =>
        _dir = Path.Combine(Path.GetTempPath(), "ga-jev-shadow-tests", Guid.NewGuid().ToString("n"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Test]
    public async Task ValidAnswer_IsLoggedWithAgreementAndRateCardCost()
    {
        JsonNode? sent = null;
        var handler = new StubHandler(async (req, ct) =>
        {
            Assert.That(req.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.typesafe.ai/v1/systemone"));
            Assert.That(req.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(req.Headers.Authorization?.Parameter, Is.EqualTo(Key));
            sent = JsonNode.Parse(await req.Content!.ReadAsStringAsync(ct));
            return Json(Answer("chordinfo"));
        });

        var record = await Shadow(handler).ObserveAsync(
            "what notes are in Cmaj7", JevRoutingShadow.Criteria(Intents), "chordinfo", 0.81, 0.07);

        Assert.That(sent!["model"]!.GetValue<string>(), Is.EqualTo(JevRoutingShadow.Model));
        Assert.That(sent["state"]!["user_message"]!.GetValue<string>(), Is.EqualTo("what notes are in Cmaj7"));
        var question = sent["questions"]!["intent"]!;
        Assert.That(question["type"]!.GetValue<string>(), Is.EqualTo("choice"));
        Assert.That(question["instructions"]!.GetValue<string>(), Is.EqualTo(JevRoutingShadow.Instructions));
        Assert.That(question["criteria"]!.AsObject().Select(kv => kv.Key),
            Is.EqualTo(new[] { "__none__", "chordinfo", "scaleinfo" }));
        Assert.That(question["criteria"]!["chordinfo"]!.GetValue<string>(), Is.EqualTo(Intents[0].Description));

        Assert.That(record.Status, Is.EqualTo("ok"));
        Assert.That(record.JevChosen, Is.EqualTo("chordinfo"));
        Assert.That(record.JevConfidence, Is.EqualTo(0.9));
        Assert.That(record.Agree, Is.True);
        Assert.That(record.InputTokens, Is.EqualTo(3000));
        Assert.That(record.CostUsd, Is.EqualTo(3000 / 1e6 * JevRoutingShadow.InputPricePerMillionUsd).Within(1e-12));
        Assert.That(record.CostKnown, Is.True);
        Assert.That(record.OptionsSha256, Has.Length.EqualTo(64));

        var log = LogLines();
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0], Does.Not.Contain(Key), "the API key must never reach the log");
        Assert.That(JsonNode.Parse(log[0])!["jev"]!.GetValue<string>(), Is.EqualTo("chordinfo"));
    }

    [Test]
    public async Task ProductionFallThrough_AgreesWithJevDeclining()
    {
        var shadow = Shadow(new StubHandler((_, _) => Task.FromResult(Json(Answer("__none__")))));

        var record = await shadow.ObserveAsync("what's the weather", JevRoutingShadow.Criteria(Intents), null, 0.41, 0.01);

        Assert.That(record.JevChosen, Is.EqualTo("__none__"));
        Assert.That(record.Agree, Is.True);
    }

    [TestCase("wrong_model", "wrong_model")]
    [TestCase("not_argmax", "invalid")]
    [TestCase("missing_option", "invalid")]
    [TestCase("sum_off", "invalid")]
    [TestCase("no_usage", "invalid")]
    [TestCase("rounded_sum", "ok")]
    public void Parse_IsFailClosed(string variant, string status)
    {
        var resp = Answer("chordinfo");
        var probs = resp["answers"]!["intent"]!["probabilities"]!.AsObject();
        switch (variant)
        {
            case "wrong_model": resp["model"] = "jev-2.0.0"; break;
            case "not_argmax": probs["scaleinfo"] = 0.95; probs["chordinfo"] = 0.0; break;
            case "missing_option": probs.Remove("scaleinfo"); probs["chordinfo"] = 0.95; break;
            case "sum_off": probs["chordinfo"] = 0.8; break;
            case "no_usage": resp.AsObject().Remove("usage"); break;
            case "rounded_sum": probs["chordinfo"] = 0.88; break; // sums to 0.98: Jev's 2-decimal rounding
        }

        using var doc = JsonDocument.Parse(resp.ToJsonString());
        var parsed = JevRoutingShadow.Parse(doc.RootElement, JevRoutingShadow.Criteria(Intents).Keys.ToHashSet());

        Assert.That(parsed.Status, Is.EqualTo(status), parsed.Error);
    }

    [Test]
    public async Task Timeout_IsLoggedAndChargedTheEstimate()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json(Answer("chordinfo"));
        });
        var shadow = Shadow(handler, timeout: TimeSpan.FromMilliseconds(100));

        var record = await shadow.ObserveAsync("slow", JevRoutingShadow.Criteria(Intents), "chordinfo", 0.8, 0.1);

        Assert.That(record.Status, Is.EqualTo("timeout"));
        Assert.That(record.CostKnown, Is.False);
        Assert.That(record.CostUsd, Is.GreaterThan(0));
        Assert.That(shadow.SpentUsd, Is.EqualTo(record.CostUsd));
    }

    [TestCase(HttpStatusCode.Unauthorized, "http_401", "skipped_stopped")]
    [TestCase(HttpStatusCode.TooManyRequests, "http_429", "skipped_stopped")]
    [TestCase(HttpStatusCode.BadGateway, "http_502", "http_502")]
    public async Task HttpError_IsNotRetried_AndAuthOrRateLimitStopsTheShadow(
        HttpStatusCode code, string first, string second)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(code)));
        var shadow = Shadow(handler);

        var a = await shadow.ObserveAsync("q1", JevRoutingShadow.Criteria(Intents), null, 0.3, null);
        var b = await shadow.ObserveAsync("q2", JevRoutingShadow.Criteria(Intents), null, 0.3, null);

        Assert.That(a.Status, Is.EqualTo(first));
        Assert.That(b.Status, Is.EqualTo(second));
        Assert.That(handler.Calls, Is.EqualTo(second == "skipped_stopped" ? 1 : 2));
    }

    [Test]
    public async Task Budget_IsCumulativeOverTheLogDirectory()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "2026-09-30.jsonl"),
            """{"ts":"x","q":"earlier","status":"ok","cost_usd":0.05}""" + "\n");
        var handler = new StubHandler((_, _) => Task.FromResult(Json(Answer("chordinfo"))));
        var shadow = Shadow(handler, budgetUsd: 0.05);

        var record = await shadow.ObserveAsync("q", JevRoutingShadow.Criteria(Intents), "chordinfo", 0.8, 0.1);

        Assert.That(record.Status, Is.EqualTo("skipped_budget"));
        Assert.That(handler.Calls, Is.Zero);
    }

    [Test]
    public async Task Observe_ReturnsBeforeTheCall_AndCapsCallsInFlight()
    {
        var release = new TaskCompletionSource();
        var handler = new StubHandler(async (_, ct) =>
        {
            await release.Task.WaitAsync(ct);
            return Json(Answer("chordinfo"));
        });
        var shadow = Shadow(handler, timeout: TimeSpan.FromSeconds(30));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < JevRoutingShadow.MaxInFlight; i++)
            shadow.Observe($"q{i}", Intents, "chordinfo", 0.8, 0.1);
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)), "Observe must not wait for Jev");
        Assert.That(LogLines(), Is.Empty);

        await WaitUntil(() => handler.Calls == JevRoutingShadow.MaxInFlight);
        var busy = await shadow.ObserveAsync("one too many", JevRoutingShadow.Criteria(Intents), "chordinfo", 0.8, 0.1);
        Assert.That(busy.Status, Is.EqualTo("skipped_busy"));

        release.SetResult();
        await WaitUntil(() => LogLines().Count == JevRoutingShadow.MaxInFlight + 1);
        Assert.That(handler.Calls, Is.EqualTo(JevRoutingShadow.MaxInFlight));
    }

    private JevRoutingShadow Shadow(StubHandler handler, double budgetUsd = 1.0, TimeSpan? timeout = null) =>
        new(new HttpClient(handler), Key, _dir, budgetUsd, timeout ?? TimeSpan.FromSeconds(5));

    private List<string> LogLines() =>
        Directory.Exists(_dir)
            ? [.. Directory.EnumerateFiles(_dir, "*.jsonl").SelectMany(File.ReadAllLines)]
            : [];

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("condition not reached within 10 s");
            await Task.Delay(20);
        }
    }

    private static JsonNode Answer(string choice)
    {
        var probs = new JsonObject { ["__none__"] = 0.05, ["chordinfo"] = 0.05, ["scaleinfo"] = 0.05 };
        probs[choice] = 0.9;
        return new JsonObject
        {
            ["model"] = JevRoutingShadow.Model,
            ["answers"] = new JsonObject
            {
                ["intent"] = new JsonObject
                {
                    ["type"] = "choice",
                    ["choice"] = choice,
                    ["confidence"] = 0.9,
                    ["probabilities"] = probs,
                },
            },
            ["usage"] = new JsonObject { ["input_tokens"] = 3000, ["output_tokens"] = 400 },
        };
    }

    private static HttpResponseMessage Json(JsonNode body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return respond(request, ct);
        }
    }

    private sealed class StubIntent(string id, string description) : IIntent
    {
        public string Id => id;
        public string Description => description;
        public IReadOnlyList<string> ExamplePrompts => ["example"];
        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
