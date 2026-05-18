namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.QaMcp.Tools;

/// <summary>
/// Tests for <see cref="QaTools.AssessBlastRadiusAt"/> — path→layer mapping,
/// component extraction, one-way-door detection, and blast scoring (Phase 1 deliverable).
/// </summary>
[TestFixture]
public class QaToolsAssessBlastRadiusTests
{
    // ── Layer mapping ────────────────────────────────────────────────────────

    [TestCase("Common/GA.Core/src/Interval.cs", "core")]
    [TestCase("Common/GA.Domain.Core/Note.cs", "core")]
    [TestCase("Common/GA.Business.Core/Analysis/Harmony.cs", "domain")]
    [TestCase("Common/GA.Business.Config/config.yaml", "domain")]
    [TestCase("Common/GA.Domain.Services/Service.cs", "domain")]
    [TestCase("Common/GA.Business.ML/Agents/QAArchitectAgent.cs", "ai_ml")]
    [TestCase("Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs", "ai_ml")]
    [TestCase("Common/GA.Business.AI/SomeClass.cs", "ai_ml")]
    [TestCase("Common/GA.Business.Core.Orchestration/Orchestrator.cs", "orchestration")]
    [TestCase("Common/GA.Business.Intelligence/Service.cs", "orchestration")]
    [TestCase("Apps/GaQaMcp/Tools/QaTools.cs", "apps")]
    [TestCase("Apps/ga-server/GaApi/Program.cs", "apps")]
    [TestCase("ga-client/src/App.tsx", "frontend")]
    [TestCase("ReactComponents/ga-react-components/index.tsx", "frontend")]
    [TestCase("docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md", "docs")]
    [TestCase("state/quality/voicing-analysis/2026-05-04.json", "docs")]
    [TestCase(".github/workflows/build.yml", "infra")]
    public void FilePathToLayer_MapsCorrectly(string path, string expectedLayer)
    {
        Assert.That(QaTools.FilePathToLayer(path), Is.EqualTo(expectedLayer),
            $"Path '{path}' should map to layer '{expectedLayer}'.");
    }

    // ── Component extraction ─────────────────────────────────────────────────

    [TestCase("Common/GA.Business.ML/Agents/QAArchitectAgent.cs", "Common/GA.Business.ML")]
    [TestCase("Apps/GaQaMcp/Tools/QaTools.cs", "Apps/GaQaMcp")]
    [TestCase("docs/plans/plan.md", "docs/plans")]
    [TestCase("README.md", "README.md")]
    public void FilePathToComponent_ExtractsTopTwoSegments(string path, string expected)
    {
        Assert.That(QaTools.FilePathToComponent(path), Is.EqualTo(expected));
    }

    // ── Full blast radius assessment with invalid SHAs ───────────────────────

    [Test]
    public void AssessBlastRadius_InvalidShas_ReturnsEmptyRadius()
    {
        // With bad SHAs, git diff fails and we get an empty changed file list.
        var json = QaTools.AssessBlastRadiusAt("guitar-alchemist/ga", "INVALID", "SHA", repoRoot: null);
        using var doc = JsonDocument.Parse(json);

        Assert.That(doc.RootElement.TryGetProperty("layers_touched", out var layers), Is.True);
        Assert.That(doc.RootElement.TryGetProperty("estimated_blast_score", out var score), Is.True);
        Assert.That(layers.EnumerateArray().Count(), Is.EqualTo(0),
            "No files changed → no layers touched.");
        Assert.That(score.GetDouble(), Is.EqualTo(0.0),
            "No files changed → blast score 0.");
    }

    [Test]
    public void AssessBlastRadius_ResponseHasAllRequiredFields()
    {
        var json = QaTools.AssessBlastRadiusAt("guitar-alchemist/ga", "INVALID", "SHA", repoRoot: null);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("layers_touched", out _), Is.True);
            Assert.That(root.TryGetProperty("one_way_doors_crossed", out _), Is.True);
            Assert.That(root.TryGetProperty("invariants_at_risk", out _), Is.True);
            Assert.That(root.TryGetProperty("components_reached", out _), Is.True);
            Assert.That(root.TryGetProperty("estimated_blast_score", out _), Is.True);
        });
    }

    // ── FindLayerViolations ─────────────────────────────────────────────────

    [Test]
    public void FindLayerViolations_EmptyDir_ReturnsEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"blast-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var violations = QaTools.FindLayerViolations(tempDir);
            Assert.That(violations, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
