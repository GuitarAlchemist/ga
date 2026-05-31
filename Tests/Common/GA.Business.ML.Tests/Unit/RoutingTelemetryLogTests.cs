namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Pins <see cref="RoutingTelemetryLog"/>: append → valid JSONL → read-back, the
/// env-dir override, and the disable switch. No router / Ollama needed — exercises
/// the sink in isolation against a temp directory.
/// </summary>
[TestFixture]
public class RoutingTelemetryLogTests
{
    private string _tempDir = "";
    private string? _priorDisable;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ga-routing-telemetry-test-" + Guid.NewGuid().ToString("N"));
        _priorDisable = Environment.GetEnvironmentVariable(RoutingTelemetryLog.DisableEnvVar);
        Environment.SetEnvironmentVariable("GA_ROUTING_TELEMETRY_DIR", _tempDir);
        Environment.SetEnvironmentVariable(RoutingTelemetryLog.DisableEnvVar, null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("GA_ROUTING_TELEMETRY_DIR", null);
        Environment.SetEnvironmentVariable(RoutingTelemetryLog.DisableEnvVar, _priorDisable);
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private static RoutingTelemetryRecord SampleMatch() => new()
    {
        Timestamp   = DateTime.UtcNow.ToString("o"),
        Query       = "common notes between F major and D minor",
        Chosen      = "skill.commontones",
        Confidence  = 0.88,
        Threshold   = 0.55,
        FellThrough = false,
        Margin      = 0.04,
        Candidates  =
        [
            new RoutingTelemetryCandidate("skill.commontones", 0.82, 0.06, 0.88),
            new RoutingTelemetryCandidate("skill.relativekey", 0.84, 0.0, 0.84),
        ],
        LatencyMs = 12.3,
    };

    [Test]
    public void Append_ThenReadRecent_RoundTripsRecord()
    {
        RoutingTelemetryLog.Append(SampleMatch());

        var read = RoutingTelemetryLog.ReadRecent();
        Assert.That(read, Has.Count.EqualTo(1));
        var r = read[0];
        Assert.That(r.Chosen, Is.EqualTo("skill.commontones"));
        Assert.That(r.FellThrough, Is.False);
        Assert.That(r.Confidence, Is.EqualTo(0.88).Within(1e-9));
        Assert.That(r.Candidates, Has.Count.EqualTo(2));
        Assert.That(r.Candidates![0].IntentId, Is.EqualTo("skill.commontones"));
        Assert.That(r.Candidates[0].Boost, Is.EqualTo(0.06).Within(1e-9));
    }

    [Test]
    public void Append_WritesExactlyOneJsonLinePerRecord()
    {
        RoutingTelemetryLog.Append(SampleMatch());
        RoutingTelemetryLog.Append(SampleMatch() with { Chosen = null, FellThrough = true, Confidence = null });

        var lines = File.ReadAllLines(RoutingTelemetryLog.CurrentDayFile());
        Assert.That(lines, Has.Length.EqualTo(2), "one JSONL line per Append");
        foreach (var line in lines)
        {
            Assert.DoesNotThrow(() => JsonDocument.Parse(line), $"each line must be valid JSON: {line}");
        }
        // Fall-through record omits null conf/chosen (WhenWritingNull) but keeps the flag.
        using var doc = JsonDocument.Parse(lines[1]);
        Assert.That(doc.RootElement.GetProperty("fell_through").GetBoolean(), Is.True);
        Assert.That(doc.RootElement.TryGetProperty("chosen", out _), Is.False, "null chosen is omitted, not serialized");
    }

    [Test]
    public void DisableEnvVar_SuppressesWrites()
    {
        Environment.SetEnvironmentVariable(RoutingTelemetryLog.DisableEnvVar, "1");
        RoutingTelemetryLog.Append(SampleMatch());
        Assert.That(RoutingTelemetryLog.ReadRecent(), Is.Empty, "GA_ROUTING_NO_TELEMETRY=1 must suppress all writes");
    }
}
