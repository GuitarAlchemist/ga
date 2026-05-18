namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.Business.ML.Embeddings;
using GA.QaMcp.Tools;

/// <summary>
/// Tests for <see cref="QaTools.VerifyInvariantsAt"/> — OPTIC-K dim check, 5-layer rule,
/// and contract-locked field assertions (Phase 1 deliverable).
/// </summary>
[TestFixture]
public class QaToolsVerifyInvariantsTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"qa-invariants-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── OPTIC-K dim invariant ───────────────────────────────────────────────

    [Test]
    public void OptickDim_InvariantPass_WhenPartitionsCoverTotalDimension()
    {
        var evidence = QaTools.CheckOptickDimInvariant();

        Assert.That(evidence.Kind, Is.EqualTo("contract_check"));
        Assert.That(evidence.Name, Is.EqualTo("optick.dim"));
        Assert.That(evidence.Outcome, Is.EqualTo("pass"),
            $"EmbeddingSchema partition coverage does not match TotalDimension={EmbeddingSchema.TotalDimension}. " +
            $"DriftSummary: {evidence.DriftSummary}");
        Assert.That(evidence.Score, Is.EqualTo(EmbeddingSchema.TotalDimension));
    }

    [Test]
    public void OptickDim_TotalDimensionIs240()
    {
        // Hard invariant: the plan references 240 as the canonical OPTIC-K-v1.8 dimension.
        // If this changes without coordinated re-index, it's a one-way-door crossing.
        Assert.That(EmbeddingSchema.TotalDimension, Is.EqualTo(240),
            "EmbeddingSchema.TotalDimension changed without coordinated re-index — one-way door crossed.");
    }

    // ── 5-layer dependency rule ─────────────────────────────────────────────

    [Test]
    public void FiveLayerRule_NoViolations_InCurrentWorkspace()
    {
        // Use the actual repo root (process CWD or repo root above Tests/).
        var repoRoot = FindRepoRoot();
        var evidence = QaTools.CheckFiveLayerRule(repoRoot);

        Assert.That(evidence.Kind, Is.EqualTo("contract_check"));
        Assert.That(evidence.Name, Is.EqualTo("five-layer.bottom-up"));
        Assert.That(evidence.Outcome, Is.EqualTo("pass"),
            $"Layer violations detected in Common/: {evidence.DriftSummary}");
    }

    [Test]
    public void FiveLayerRule_FindsViolation_WhenLowLayerRefersToHighLayer()
    {
        // Create a fake Common/ structure with a violation: GA.Core references GA.Business.ML.
        var commonDir = Path.Combine(_tempRoot, "Common", "GA.Core");
        Directory.CreateDirectory(commonDir);
        File.WriteAllText(Path.Combine(commonDir, "GA.Core.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\GA.Business.ML\GA.Business.ML.csproj" />
              </ItemGroup>
            </Project>
            """);
        var mlDir = Path.Combine(_tempRoot, "Common", "GA.Business.ML");
        Directory.CreateDirectory(mlDir);
        File.WriteAllText(Path.Combine(mlDir, "GA.Business.ML.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
            </Project>
            """);

        var violations = QaTools.FindLayerViolations(_tempRoot);

        Assert.That(violations, Has.Some.Contains("GA.Core").And.Contains("GA.Business.ML"),
            "Expected to find a L1→L4 violation but got: " + string.Join("; ", violations));
    }

    [Test]
    public void FiveLayerRule_NoViolation_WhenHighLayerRefersToLowLayer()
    {
        // GA.Business.ML referencing GA.Core is legal (L4 → L1, downward reference).
        var mlDir = Path.Combine(_tempRoot, "Common", "GA.Business.ML");
        Directory.CreateDirectory(mlDir);
        File.WriteAllText(Path.Combine(mlDir, "GA.Business.ML.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\GA.Core\GA.Core.csproj" />
              </ItemGroup>
            </Project>
            """);
        var coreDir = Path.Combine(_tempRoot, "Common", "GA.Core");
        Directory.CreateDirectory(coreDir);
        File.WriteAllText(Path.Combine(coreDir, "GA.Core.csproj"), "<Project />");

        var violations = QaTools.FindLayerViolations(_tempRoot);

        Assert.That(violations, Is.Empty,
            $"GA.Business.ML → GA.Core is a valid downward reference. Got: {string.Join("; ", violations)}");
    }

    // ── Contract-locked field checks ────────────────────────────────────────

    [Test]
    public void ContractLockedFields_SchemaVersionAndEmbeddingVersion_Pass()
    {
        var evidences = QaTools.CheckContractLockedFields(_tempRoot);

        Assert.That(evidences, Is.Not.Empty);
        foreach (var e in evidences)
        {
            Assert.That(e.Outcome, Is.EqualTo("pass"),
                $"Contract-locked field check '{e.Name}' failed: {e.DriftSummary}");
        }
    }

    // ── Full VerifyInvariantsAt integration ─────────────────────────────────

    [Test]
    public void VerifyInvariantsAt_ReturnsEvidenceArray_WithRequiredKinds()
    {
        var repoRoot = FindRepoRoot();
        var json = QaTools.VerifyInvariantsAt("phase1-test", repoRoot);
        using var doc = JsonDocument.Parse(json);

        Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        var items = doc.RootElement.EnumerateArray().ToList();
        Assert.That(items.Count, Is.GreaterThanOrEqualTo(3),
            "Expected at least 3 evidence items (optick-dim, five-layer, contract-fields).");
        Assert.That(items.All(i => i.TryGetProperty("kind", out _)), Is.True);
        Assert.That(items.All(i => i.TryGetProperty("name", out _)), Is.True);
        Assert.That(items.All(i => i.TryGetProperty("outcome", out _)), Is.True);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (dir.GetFiles("AllProjects.slnx").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
