namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.QaMcp.Tools;

/// <summary>
/// Tests for <see cref="QaTools.ScoreQualityDriftAt"/> covering the voicing-analysis
/// and embeddings time-series branches (Phase 1 deliverable).
/// </summary>
[TestFixture]
public class QaToolsVoicingDriftTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"qa-voicing-drift-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── voicing-analysis branch ─────────────────────────────────────────────

    [Test]
    public void VoicingAnalysis_NoDirectory_ReturnsNotApplicable()
    {
        var result = QaTools.ScoreQualityDriftAt("voicing-analysis", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("kind").GetString(), Is.EqualTo("quality_snapshot"));
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("voicing-analysis"));
        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("n/a"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("No voicing-analysis"));
    }

    [Test]
    public void VoicingAnalysis_TwoStableSnapshots_OutcomePass()
    {
        WriteVoicingSnapshot("2026-04-29", DaysAgo(5), corpusTotal: 310000, invariantFailures: 50);
        WriteVoicingSnapshot("2026-05-04", DaysAgo(0), corpusTotal: 313047, invariantFailures: 50);

        var result = QaTools.ScoreQualityDriftAt("voicing-analysis", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("pass"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("within tolerance"));
    }

    [Test]
    public void VoicingAnalysis_CorpusShrinks_OutcomeConcern()
    {
        WriteVoicingSnapshot("2026-04-29", DaysAgo(5), corpusTotal: 313047, invariantFailures: 0);
        WriteVoicingSnapshot("2026-05-04", DaysAgo(0), corpusTotal: 100000, invariantFailures: 0);

        var result = QaTools.ScoreQualityDriftAt("voicing-analysis", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("concern"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("corpus shrank"));
    }

    [Test]
    public void VoicingAnalysis_InvariantFailuresSurge_OutcomeConcern()
    {
        WriteVoicingSnapshot("2026-04-29", DaysAgo(5), corpusTotal: 313047, invariantFailures: 0);
        WriteVoicingSnapshot("2026-05-04", DaysAgo(0), corpusTotal: 313047, invariantFailures: 100);

        var result = QaTools.ScoreQualityDriftAt("voicing-analysis", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("concern"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("invariant_failures"));
    }

    [Test]
    public void VoicingAnalysis_OneSnapshot_ReturnsNotApplicable()
    {
        WriteVoicingSnapshot("2026-05-04", DaysAgo(0), corpusTotal: 313047, invariantFailures: 50);

        var result = QaTools.ScoreQualityDriftAt("voicing-analysis", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("n/a"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain(">=2 snapshots"));
    }

    [Test]
    public void VoicingAnalysis_CaseInsensitiveMetric_Works()
    {
        var result = QaTools.ScoreQualityDriftAt("VOICING-ANALYSIS", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        // No dir → n/a, but must still hit the voicing-analysis branch, not the unknown-metric fallback.
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("voicing-analysis"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Not.Contain("Unknown metric"));
    }

    // ── embeddings branch ───────────────────────────────────────────────────

    [Test]
    public void Embeddings_NoDirectory_ReturnsNotApplicable()
    {
        var result = QaTools.ScoreQualityDriftAt("embeddings", 30, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("embeddings"));
        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("n/a"));
    }

    [Test]
    public void Embeddings_TwoStableSnapshots_OutcomePass()
    {
        WriteEmbeddingSnapshot("2026-04-17.json", DaysAgo(30), dims: 112, classifierAccuracy: 0.747);
        WriteEmbeddingSnapshot("2026-04-18.json", DaysAgo(29), dims: 112, classifierAccuracy: 0.750);

        var result = QaTools.ScoreQualityDriftAt("embeddings", 60, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("pass"));
    }

    [Test]
    public void Embeddings_DimsChange_OutcomeConcern()
    {
        WriteEmbeddingSnapshot("2026-04-17.json", DaysAgo(30), dims: 112, classifierAccuracy: 0.747);
        WriteEmbeddingSnapshot("2026-04-18.json", DaysAgo(29), dims: 124, classifierAccuracy: 0.747);

        var result = QaTools.ScoreQualityDriftAt("embeddings", 60, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("concern"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("dims changed"));
    }

    // ── unknown metric fallback ─────────────────────────────────────────────

    [Test]
    public void UnknownMetric_ReturnsUnknownMetricDriftSummary()
    {
        var result = QaTools.ScoreQualityDriftAt("nonexistent-metric", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("Unknown metric"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string DaysAgo(int days) =>
        DateTimeOffset.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private void WriteVoicingSnapshot(string fileName, string timestamp, long corpusTotal, int invariantFailures)
    {
        var dir = Path.Combine(_tempRoot, "voicing-analysis");
        Directory.CreateDirectory(dir);
        var json = $$"""
        {
          "Timestamp": "{{timestamp}}",
          "Corpus": { "Total": {{corpusTotal}} },
          "InvariantFailures": {
            "MidiNotesMismatch": 0,
            "NullPitchClassSet": 0,
            "NegativePhysicalLayout": 0,
            "IntervalSpreadInvariant": {{invariantFailures}}
          },
          "AnalyzerExceptions": []
        }
        """;
        File.WriteAllText(Path.Combine(dir, fileName + ".json"), json);
    }

    private void WriteEmbeddingSnapshot(string fileName, string timestamp, int dims, double classifierAccuracy)
    {
        var dir = Path.Combine(_tempRoot, "embeddings");
        Directory.CreateDirectory(dir);
        var json = $$"""
        {
          "timestamp": "{{timestamp}}",
          "tool": "ix-embedding-diagnostics 0.1.0",
          "corpus": { "dims": {{dims}} },
          "leak_detection": {
            "full_classifier_accuracy": {{classifierAccuracy.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)}}
          }
        }
        """;
        File.WriteAllText(Path.Combine(dir, fileName), json);
    }
}
