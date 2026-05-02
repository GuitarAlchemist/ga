namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.Business.ML.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestFixture]
public class QaArchitectAgentTests
{
    private Mock<IChatClient> _chatClient = null!;

    [SetUp]
    public void Setup() => _chatClient = new Mock<IChatClient>();

    [Test]
    public async Task ProcessAsync_ReturnsContractValidVerdictInData()
    {
        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance);
        var request = new AgentRequest { Query = "Phase 0 smoke" };

        var response = await agent.ProcessAsync(request);

        Assert.That(response.AgentId, Is.EqualTo(AgentIds.QaArchitect));
        Assert.That(response.Data, Is.InstanceOf<QaVerdict>());
        var verdict = (QaVerdict)response.Data!;
        Assert.That(verdict.SchemaVersion, Is.EqualTo(1));
        Assert.That(verdict.Producer, Is.EqualTo("qa-architect-agent"));
        Assert.That(verdict.RiskTier, Is.EqualTo("P3"));
        Assert.That(verdict.Verdict, Is.EqualTo("informational"));
        Assert.That(verdict.ReviewerChain, Is.Not.Empty);
        Assert.That(verdict.Narrative, Is.Not.Empty);
    }

    [Test]
    public async Task Verdict_RoundTripsThroughJsonWithoutLoss()
    {
        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance);
        var response = await agent.ProcessAsync(new AgentRequest { Query = "Round-trip" });
        var original = (QaVerdict)response.Data!;

        var json = QaVerdictJson.Serialize(original);
        var deserialized = QaVerdictJson.Deserialize(json);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.SchemaVersion, Is.EqualTo(original.SchemaVersion));
        Assert.That(deserialized.VerdictId, Is.EqualTo(original.VerdictId));
        Assert.That(deserialized.Producer, Is.EqualTo(original.Producer));
        Assert.That(deserialized.RiskTier, Is.EqualTo(original.RiskTier));
        Assert.That(deserialized.Verdict, Is.EqualTo(original.Verdict));
        Assert.That(deserialized.Target.Repo, Is.EqualTo(original.Target.Repo));
        Assert.That(deserialized.BlastRadius.EstimatedBlastScore, Is.EqualTo(original.BlastRadius.EstimatedBlastScore));
        Assert.That(deserialized.Evidence.Count, Is.EqualTo(original.Evidence.Count));
        Assert.That(deserialized.ReviewerChain.Count, Is.EqualTo(original.ReviewerChain.Count));
        Assert.That(deserialized.Narrative, Is.EqualTo(original.Narrative));
    }

    [Test]
    public async Task Verdict_SerializesWithSnakeCaseKeysMatchingContract()
    {
        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance);
        var response = await agent.ProcessAsync(new AgentRequest { Query = "Key check" });
        var verdict = (QaVerdict)response.Data!;

        var json = QaVerdictJson.Serialize(verdict);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Required snake_case keys per docs/contracts/qa-verdict.schema.json
        string[] required =
        [
            "schema_version", "verdict_id", "produced_at", "producer", "producer_version",
            "target", "risk_tier", "verdict", "blast_radius", "evidence", "followups",
            "reviewer_chain", "narrative"
        ];
        foreach (var key in required)
            Assert.That(root.TryGetProperty(key, out _), Is.True, $"Missing required key: {key}");

        Assert.That(root.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("blast_radius").TryGetProperty("estimated_blast_score", out _), Is.True);
    }

    [Test]
    public async Task Verdict_PersistsAndReloadsFromTempFile()
    {
        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance);
        var response = await agent.ProcessAsync(new AgentRequest { Query = "Persistence" });
        var original = (QaVerdict)response.Data!;

        var dir = Path.Combine(Path.GetTempPath(), $"ga-qa-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, $"{original.VerdictId}.json");
            File.WriteAllText(path, QaVerdictJson.Serialize(original));

            var reloaded = QaVerdictJson.Deserialize(File.ReadAllText(path));
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded!.VerdictId, Is.EqualTo(original.VerdictId));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
