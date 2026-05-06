namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.QaMcp.Tools;

/// <summary>
/// Unit tests for <see cref="QaTools.ScoreQualityDriftAt"/> covering the Phase 1
/// SAE-detection branch and the Phase 2 drift-computation branch
/// (reconstruction_mse / dead_features_pct / partition_purity deltas across
/// consecutive artifacts in a window).
/// </summary>
[TestFixture]
public class QaToolsScoreQualityDriftTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"qa-tools-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── Phase 1 SAE-branch detection (preserved) ─────────────────────────────

    [Test]
    public void OptickSae_NoArtifacts_ReturnsNotApplicable()
    {
        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);

        var root = JsonDocument.Parse(result).RootElement;
        Assert.That(root.GetProperty("kind").GetString(), Is.EqualTo("quality_snapshot"));
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("optick-sae"));
        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("n/a"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("No SAE artifacts"));
    }

    [Test]
    public void OptickSae_CaseInsensitiveMetric_TriggersSaeBranch()
    {
        var result = QaTools.ScoreQualityDriftAt("OPTICK-SAE", 7, _tempRoot);

        var root = JsonDocument.Parse(result).RootElement;
        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("n/a"),
            "Case-insensitive match means OPTICK-SAE hits the SAE branch.");
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("No SAE artifacts"));
    }

    [Test]
    public void OtherMetric_FallsThroughToPhase0Stub()
    {
        var result = QaTools.ScoreQualityDriftAt("voicing-analysis", 14, _tempRoot);

        var root = JsonDocument.Parse(result).RootElement;
        Assert.That(root.GetProperty("kind").GetString(), Is.EqualTo("quality_snapshot"));
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("voicing-analysis"));
        var summary = root.GetProperty("drift_summary").GetString();
        Assert.That(summary, Does.Not.Contain("SAE artifact"));
        Assert.That(summary, Does.Contain("skeleton").Or.Contain("Phase 0"));
    }

    // ── Phase 2 drift computation ───────────────────────────────────────────

    [Test]
    public void Drift_OneArtifact_ReportsInsufficientHistory()
    {
        WriteArtifact("2026-05-04", DaysAgo(1), mse: 0.001, dead: 10.0, purity: 0.50);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("n/a"));
        Assert.That(root.GetProperty("drift_summary").GetString(),
            Does.Contain("Only one SAE artifact").And.Contain("≥2"));
    }

    [Test]
    public void Drift_TwoArtifacts_StableMetrics_OutcomePass()
    {
        WriteArtifact("2026-04-29", DaysAgo(5), mse: 0.001, dead: 10.0, purity: 0.50);
        WriteArtifact("2026-05-04", DaysAgo(0), mse: 0.0011, dead: 10.5, purity: 0.49);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("pass"));
        Assert.That(root.GetProperty("score").GetDouble(), Is.EqualTo(0.0011).Within(1e-9));
        Assert.That(root.GetProperty("baseline").GetDouble(), Is.EqualTo(0.001).Within(1e-9));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("within tolerance"));
    }

    [Test]
    public void Drift_MseJumpAboveTolerance_OutcomeConcern()
    {
        // +100% MSE vs baseline — well above the 50% relative tolerance.
        WriteArtifact("2026-04-29", DaysAgo(5), mse: 0.001, dead: 10.0, purity: 0.50);
        WriteArtifact("2026-05-04", DaysAgo(0), mse: 0.002, dead: 10.0, purity: 0.50);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("concern"));
        Assert.That(root.GetProperty("drift_summary").GetString(),
            Does.Contain("reconstruction_mse").And.Contain("100%"));
        Assert.That(root.GetProperty("delta_from_baseline").GetDouble(), Is.EqualTo(0.001).Within(1e-9));
    }

    [Test]
    public void Drift_DeadFeaturesAboveTolerance_OutcomeConcern()
    {
        // +10 pct points dead features — above the 5pp tolerance.
        WriteArtifact("2026-04-29", DaysAgo(5), mse: 0.001, dead: 10.0, purity: 0.50);
        WriteArtifact("2026-05-04", DaysAgo(0), mse: 0.001, dead: 20.0, purity: 0.50);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("concern"));
        Assert.That(root.GetProperty("drift_summary").GetString(),
            Does.Contain("dead_features_pct"));
    }

    [Test]
    public void Drift_PurityDropAboveTolerance_OutcomeConcern()
    {
        // -0.10 absolute purity drop — above the 0.05 tolerance.
        WriteArtifact("2026-04-29", DaysAgo(5), mse: 0.001, dead: 10.0, purity: 0.50);
        WriteArtifact("2026-05-04", DaysAgo(0), mse: 0.001, dead: 10.0, purity: 0.40);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("concern"));
        Assert.That(root.GetProperty("drift_summary").GetString(),
            Does.Contain("feature_partition_purity_mean"));
    }

    [Test]
    public void Drift_LocalSuffixDirs_AreExcluded()
    {
        // Two -local artifacts should be ignored (per-developer experiments,
        // not the shared timeline). With both excluded, drift falls back to "n/a".
        WriteArtifact("2026-04-29-local", DaysAgo(5), mse: 0.001, dead: 10.0, purity: 0.50);
        WriteArtifact("2026-05-04-local", DaysAgo(0), mse: 0.50, dead: 90.0, purity: 0.01);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("n/a"),
            "All -local artifacts should be filtered out, leaving zero in window.");
    }

    [Test]
    public void Drift_ArtifactsOutsideWindow_AreExcluded()
    {
        // Old artifact (30d ago) outside a 7d window; only newest counts → insufficient history.
        WriteArtifact("2026-04-04", DaysAgo(30), mse: 0.001, dead: 10.0, purity: 0.50);
        WriteArtifact("2026-05-04", DaysAgo(0), mse: 0.001, dead: 10.0, purity: 0.50);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("n/a"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("Only one SAE artifact"));
    }

    [Test]
    public void Drift_MalformedArtifact_IsSkipped()
    {
        // A corrupt artifact JSON should be silently skipped, not crash the tool.
        WriteArtifact("2026-04-29", DaysAgo(5), mse: 0.001, dead: 10.0, purity: 0.50);
        var corruptDir = Path.Combine(_tempRoot, "optick-sae", "2026-05-01");
        Directory.CreateDirectory(corruptDir);
        File.WriteAllText(Path.Combine(corruptDir, "optick-sae-artifact.json"), "{ not valid json");
        WriteArtifact("2026-05-04", DaysAgo(0), mse: 0.0011, dead: 10.5, purity: 0.49);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.That(root.GetProperty("outcome").GetString(), Is.EqualTo("pass"),
            "Two well-formed artifacts should drive the outcome; the malformed one is skipped.");
    }

    [Test]
    public void Drift_EvidenceShape_MatchesContract()
    {
        WriteArtifact("2026-04-29", DaysAgo(5), mse: 0.001, dead: 10.0, purity: 0.50);
        WriteArtifact("2026-05-04", DaysAgo(0), mse: 0.0011, dead: 10.5, purity: 0.49);

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var root = JsonDocument.Parse(result).RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("kind", out _), Is.True);
            Assert.That(root.TryGetProperty("name", out _), Is.True);
            Assert.That(root.TryGetProperty("outcome", out _), Is.True);
            Assert.That(root.TryGetProperty("score", out _), Is.True);
            Assert.That(root.TryGetProperty("baseline", out _), Is.True);
            Assert.That(root.TryGetProperty("delta_from_baseline", out _), Is.True);
            Assert.That(root.TryGetProperty("guardrail_max", out _), Is.True);
            Assert.That(root.TryGetProperty("drift_summary", out _), Is.True);
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp <paramref name="days"/> ago, ISO-8601.</summary>
    private static string DaysAgo(int days) =>
        DateTimeOffset.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");

    /// <summary>
    /// Writes a minimally-shaped SAE artifact to
    /// <c>{_tempRoot}/optick-sae/{dateDir}/optick-sae-artifact.json</c> with
    /// the metrics fields the drift logic reads.
    /// </summary>
    private void WriteArtifact(string dateDir, string trainedAtIso, double mse, double dead, double purity)
    {
        var dir = Path.Combine(_tempRoot, "optick-sae", dateDir);
        Directory.CreateDirectory(dir);
        var artifactId = $"optick-sae-{trainedAtIso.Replace(':', '-')}-test{Guid.NewGuid():N}-topk-sae".Replace(":", "-");
        var json = $$"""
        {
          "artifact_id": "{{artifactId}}",
          "trained_at": "{{trainedAtIso}}",
          "metrics": {
            "reconstruction_mse": {{mse.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)}},
            "dead_features_pct": {{dead.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)}},
            "feature_partition_purity_mean": {{purity.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)}}
          }
        }
        """;
        File.WriteAllText(Path.Combine(dir, "optick-sae-artifact.json"), json);
    }
}
