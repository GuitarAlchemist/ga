namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.Business.ML.Agents;
using GA.QaMcp.Tools;

/// <summary>
/// Tests for <see cref="QaTools.EmitVerdictAt"/> — contract §4 storage layout
/// (<c>state/quality/verdicts/&lt;repo&gt;/&lt;ref&gt;/&lt;verdict_id&gt;.json</c>).
/// Phase 1 replaces the Phase 0 temp-directory write with the proper layout.
/// </summary>
[TestFixture]
public class QaToolsEmitVerdictTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"qa-emit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void EmitVerdict_DefaultSkeleton_WritesToContractLayout()
    {
        var json = QaTools.EmitVerdictAt(verdictJson: null, verdictRoot: _tempRoot);
        using var result = JsonDocument.Parse(json);

        Assert.That(result.RootElement.TryGetProperty("verdict_id", out var idEl), Is.True);
        Assert.That(result.RootElement.TryGetProperty("persisted_path", out var pathEl), Is.True);

        var path = pathEl.GetString()!;
        Assert.That(File.Exists(path), Is.True, $"Verdict file not found at {path}");
        // Contract §4: .../<repo>/<ref>/<verdict_id>.json
        Assert.That(path, Does.Contain("guitar-alchemist"),
            "Path should contain the repo org segment.");
    }

    [Test]
    public void EmitVerdict_RepoSegment_UsesNestedDirNotUnderscore()
    {
        // Contract §4 says state/quality/verdicts/<repo>/<ref>/<id>.json
        // repo = "guitar-alchemist/ga" → two path segments, not "guitar-alchemist_ga"
        var json = QaTools.EmitVerdictAt(verdictJson: null, verdictRoot: _tempRoot);
        using var result = JsonDocument.Parse(json);

        var path = result.RootElement.GetProperty("persisted_path").GetString()!;
        var relative = Path.GetRelativePath(_tempRoot, path);
        var segments = relative.Split(Path.DirectorySeparatorChar);

        // Expect at least: <org>/<name>/<ref>/<verdict_id>.json → 4 segments
        Assert.That(segments.Length, Is.GreaterThanOrEqualTo(4),
            $"Expected ≥4 path segments under verdictRoot. Got: {relative}");
        Assert.That(segments[0], Is.EqualTo("guitar-alchemist"));
        Assert.That(segments[1], Is.EqualTo("ga"));
    }

    [Test]
    public void EmitVerdict_VerdictId_IsFilenamesafe()
    {
        var json = QaTools.EmitVerdictAt(verdictJson: null, verdictRoot: _tempRoot);
        using var result = JsonDocument.Parse(json);
        var id = result.RootElement.GetProperty("verdict_id").GetString()!;

        // No colons (breaks Windows), no spaces, no slashes.
        Assert.That(id, Does.Not.Contain(":"), "verdict_id must not contain colons.");
        Assert.That(id, Does.Not.Contain(" "), "verdict_id must not contain spaces.");
        Assert.That(id, Does.Not.Contain("/"), "verdict_id must not contain forward slashes.");
        Assert.That(id, Does.Not.Contain("\\"), "verdict_id must not contain backslashes.");
    }

    [Test]
    public void EmitVerdict_PersistedFile_RoundTripsAsValidVerdict()
    {
        var json = QaTools.EmitVerdictAt(verdictJson: null, verdictRoot: _tempRoot);
        using var result = JsonDocument.Parse(json);
        var path = result.RootElement.GetProperty("persisted_path").GetString()!;

        var content = File.ReadAllText(path);
        var reloaded = QaVerdictJson.Deserialize(content);

        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded!.SchemaVersion, Is.EqualTo(1));
        Assert.That(reloaded.Producer, Is.EqualTo("qa-architect-agent"));
    }

    [Test]
    public void EmitVerdict_WithExplicitVerdictJson_PersistsCorrectly()
    {
        var request = new AgentRequest { Query = "explicit emit test" };
        var verdict = QAArchitectAgent.BuildSkeletonVerdict(request);
        var verdictJson = QaVerdictJson.Serialize(verdict);

        var json = QaTools.EmitVerdictAt(verdictJson, verdictRoot: _tempRoot);
        using var result = JsonDocument.Parse(json);

        Assert.That(result.RootElement.GetProperty("verdict_id").GetString(),
            Is.EqualTo(verdict.VerdictId));
        var path = result.RootElement.GetProperty("persisted_path").GetString()!;
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void EmitVerdict_RefWithSpecialChars_CreatesFilenameSafePath()
    {
        // Build a verdict with a ref containing slashes (e.g. "pr/1234").
        var request = new AgentRequest { Query = "slash-ref test" };
        var skeleton = QAArchitectAgent.BuildSkeletonVerdict(request);
        // Recreate with a slash-containing ref.
        var verdict = skeleton with
        {
            Target = skeleton.Target with { Ref = "pr/1234" }
        };
        var json = QaTools.EmitVerdictAt(QaVerdictJson.Serialize(verdict), verdictRoot: _tempRoot);
        using var result = JsonDocument.Parse(json);

        var path = result.RootElement.GetProperty("persisted_path").GetString()!;
        Assert.That(File.Exists(path), Is.True, $"File should exist at {path}");
    }
}
