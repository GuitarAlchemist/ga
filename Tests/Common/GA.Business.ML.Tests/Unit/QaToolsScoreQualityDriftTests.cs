namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.QaMcp.Tools;

/// <summary>
/// Unit tests for the Phase 1 stub branch in <see cref="QaTools.ScoreQualityDriftAt"/>.
/// PR #82 added the "optick-sae" metric special-case logic but shipped without coverage;
/// this class is the regression guard.
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

    [Test]
    public void OptickSae_NoArtifacts_ReturnsExpectedDriftSummary()
    {
        // No optick-sae directory exists at all.
        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        Assert.That(root.GetProperty("kind").GetString(), Is.EqualTo("quality_snapshot"));
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("optick-sae"));
        Assert.That(root.GetProperty("drift_summary").GetString(), Does.Contain("No SAE artifacts found"));
    }

    [Test]
    public void OptickSae_OneArtifact_ReportsCount()
    {
        var saeDir = Path.Combine(_tempRoot, "optick-sae", "2026-05-04");
        Directory.CreateDirectory(saeDir);
        File.WriteAllText(Path.Combine(saeDir, "optick-sae-artifact.json"), "{}");

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);

        var doc = JsonDocument.Parse(result);
        var summary = doc.RootElement.GetProperty("drift_summary").GetString();
        Assert.That(summary, Does.Contain("SAE artifact found"));
        Assert.That(summary, Does.Contain("(1 run(s)"));
    }

    [Test]
    public void OptickSae_MultipleArtifacts_ReportsCorrectCount()
    {
        // Three artifacts across three different date directories — supersedes chain.
        foreach (var date in (string[])["2026-05-03", "2026-05-04", "2026-05-05"])
        {
            var dir = Path.Combine(_tempRoot, "optick-sae", date);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "optick-sae-artifact.json"), "{}");
        }

        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);

        var doc = JsonDocument.Parse(result);
        var summary = doc.RootElement.GetProperty("drift_summary").GetString();
        Assert.That(summary, Does.Contain("(3 run(s)"));
    }

    [Test]
    public void OptickSae_CaseInsensitiveMetric_TriggersSaeBranch()
    {
        // The metric switch is case-insensitive — verify "OPTICK-SAE" hits the same path.
        var result = QaTools.ScoreQualityDriftAt("OPTICK-SAE", 7, _tempRoot);

        var doc = JsonDocument.Parse(result);
        var summary = doc.RootElement.GetProperty("drift_summary").GetString();
        Assert.That(summary, Does.Contain("No SAE artifacts found"),
            "Case-insensitive match means OPTICK-SAE should hit the SAE branch (not the default phase 0 stub).");
    }

    [Test]
    public void OtherMetric_FallsThroughToPhase0Stub()
    {
        // Metrics other than "optick-sae" should hit the phase 0 fall-through path.
        var result = QaTools.ScoreQualityDriftAt("voicing-analysis", 14, _tempRoot);

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        Assert.That(root.GetProperty("kind").GetString(), Is.EqualTo("quality_snapshot"));
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("voicing-analysis"));
        // Phase 0 stub shape: drift_summary mentions skeleton state, no artifact-found language.
        var summary = root.GetProperty("drift_summary").GetString();
        Assert.That(summary, Does.Not.Contain("SAE artifact"));
        Assert.That(summary, Does.Contain("skeleton").Or.Contain("Phase 0"));
    }

    [Test]
    public void OptickSae_DriftSummaryIsContractEvidenceShape()
    {
        // The returned JSON should be parseable as a QaEvidence record per the
        // qa-verdict.contract.md §3.5: kind, name, drift_summary at minimum.
        var result = QaTools.ScoreQualityDriftAt("optick-sae", 7, _tempRoot);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("kind", out _), Is.True, "missing 'kind'");
            Assert.That(root.TryGetProperty("name", out _), Is.True, "missing 'name'");
            Assert.That(root.TryGetProperty("drift_summary", out _), Is.True, "missing 'drift_summary'");
        });
    }
}
