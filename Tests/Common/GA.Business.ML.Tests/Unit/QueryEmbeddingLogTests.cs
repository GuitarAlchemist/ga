namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Pins <see cref="QueryEmbeddingLog"/> (Contract B → ix-duck OOD lens): append →
/// valid JSONL with the ratified field names, the exact vector round-trips, the
/// declined-row shape (null intent omitted, route_method=fallback), the env-dir
/// override, and the disable switch. No router / Ollama needed — exercises the sink
/// in isolation against a temp directory.
/// </summary>
[TestFixture]
public class QueryEmbeddingLogTests
{
    private string _tempDir = "";
    private string? _priorDisable;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ga-query-embedding-test-" + Guid.NewGuid().ToString("N"));
        _priorDisable = Environment.GetEnvironmentVariable(QueryEmbeddingLog.DisableEnvVar);
        Environment.SetEnvironmentVariable("GA_QUERY_EMBEDDING_DIR", _tempDir);
        Environment.SetEnvironmentVariable(QueryEmbeddingLog.DisableEnvVar, null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("GA_QUERY_EMBEDDING_DIR", null);
        Environment.SetEnvironmentVariable(QueryEmbeddingLog.DisableEnvVar, _priorDisable);
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private static QueryEmbeddingRecord SampleRouted() => new()
    {
        QueryId         = Guid.NewGuid().ToString("n"),
        Timestamp       = DateTime.UtcNow.ToString("o"),
        QueryText       = "are 0146 and 0137 z-related?",
        Intent          = "skill.zrelation",
        RouteMethod     = "embedding",
        RouteConfidence = 0.82,
        Embedder        = "bge-large",
        Dim             = 4,
        Embedding       = [0.10f, -0.20f, 0.30f, 0.40f],
    };

    [Test]
    public void Append_WritesRatifiedFieldNames_AndRoundTripsVector()
    {
        QueryEmbeddingLog.Append(SampleRouted());

        var lines = File.ReadAllLines(QueryEmbeddingLog.CurrentDayFile());
        Assert.That(lines, Has.Length.EqualTo(1), "one JSONL line per Append");

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        // The cross-repo contract is the FIELD NAMES — ix-duck reads these.
        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("query_id", out _), Is.True);
            Assert.That(root.GetProperty("query_text").GetString(), Is.EqualTo("are 0146 and 0137 z-related?"));
            Assert.That(root.GetProperty("intent").GetString(), Is.EqualTo("skill.zrelation"));
            Assert.That(root.GetProperty("route_method").GetString(), Is.EqualTo("embedding"));
            Assert.That(root.GetProperty("route_confidence").GetDouble(), Is.EqualTo(0.82).Within(1e-9));
            Assert.That(root.GetProperty("embedder").GetString(), Is.EqualTo("bge-large"));
            Assert.That(root.GetProperty("dim").GetInt32(), Is.EqualTo(4));
        });

        var vec = root.GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        Assert.That(vec, Is.EqualTo(new[] { 0.10f, -0.20f, 0.30f, 0.40f }).Within(1e-6f),
            "the EXACT vector the router scored must round-trip — the OOD lens analyses it");
        Assert.That(vec.Length, Is.EqualTo(root.GetProperty("dim").GetInt32()), "dim must match the vector length");
    }

    [Test]
    public void DeclinedRow_OmitsNullIntent_AndMarksFallback()
    {
        QueryEmbeddingLog.Append(SampleRouted() with { Intent = null, RouteMethod = "fallback" });

        using var doc = JsonDocument.Parse(File.ReadAllLines(QueryEmbeddingLog.CurrentDayFile())[0]);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.TryGetProperty("intent", out _), Is.False, "null intent is omitted, not serialized");
            Assert.That(doc.RootElement.GetProperty("route_method").GetString(), Is.EqualTo("fallback"));
            // A declined query STILL records its confidence + vector — that's the OOD signal.
            Assert.That(doc.RootElement.GetProperty("embedding").GetArrayLength(), Is.EqualTo(4));
        });
    }

    [Test]
    public void DisableEnvVar_SuppressesWrites()
    {
        Environment.SetEnvironmentVariable(QueryEmbeddingLog.DisableEnvVar, "1");
        QueryEmbeddingLog.Append(SampleRouted());
        Assert.That(File.Exists(QueryEmbeddingLog.CurrentDayFile()), Is.False,
            "GA_QUERY_EMBEDDING_NO_LOG=1 must suppress all writes");
    }
}
