namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Phase 1 tests for <see cref="QAArchitectAgent"/> — CriticAgent wiring as
/// semantic_judge, reviewer_chain enrichment, and graceful fallback when no
/// Coordinator is present.
/// </summary>
[TestFixture]
public class QaArchitectAgentPhase1Tests
{
    private Mock<IChatClient> _chatClient = null!;

    [SetUp]
    public void Setup() => _chatClient = new Mock<IChatClient>();

    // ── No coordinator → skeleton-only reviewer chain ────────────────────────

    [Test]
    public async Task ProcessAsync_NoCoordinator_ReviewerChainHasOnlyArchitectEntry()
    {
        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance);
        var request = new AgentRequest { Query = "diff: +1 line in analysis layer" };

        var response = await agent.ProcessAsync(request);

        var verdict = (QaVerdict)response.Data!;
        Assert.That(verdict.ReviewerChain, Has.Count.EqualTo(1),
            "Without a Coordinator, only the self-emit entry should be in the chain.");
        Assert.That(verdict.ReviewerChain[0].Agent, Is.EqualTo("ga.QAArchitectAgent"));
    }

    [Test]
    public async Task ProcessAsync_EmptyQuery_NoCoordinator_StillReturnsInformationalVerdict()
    {
        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance);
        var request = new AgentRequest { Query = "" };

        var response = await agent.ProcessAsync(request);

        var verdict = (QaVerdict)response.Data!;
        Assert.That(verdict.Verdict, Is.EqualTo("informational"));
        Assert.That(verdict.RiskTier, Is.EqualTo("P3"));
    }

    // ── With coordinator → CriticAgent appended to reviewer chain ───────────

    [Test]
    public async Task ProcessAsync_WithCoordinator_AppendsCriticAgentToReviewerChain()
    {
        var coordinator = new Mock<IAgentCoordinator>();
        coordinator
            .Setup(c => c.DelegateAsync(It.IsAny<string>(), AgentIds.Critic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse
            {
                AgentId = AgentIds.Critic,
                Result = "Semantic review complete. No contradictions detected.",
                Confidence = 0.88f
            });

        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance)
        {
            Coordinator = coordinator.Object
        };

        var response = await agent.ProcessAsync(new AgentRequest { Query = "diff context: real code changed" });
        var verdict = (QaVerdict)response.Data!;

        Assert.That(verdict.ReviewerChain.Count, Is.GreaterThanOrEqualTo(2),
            "With a wired Coordinator, CriticAgent should be appended to the reviewer_chain.");
        var judge = verdict.ReviewerChain.FirstOrDefault(r => r.Role == "semantic_judge");
        Assert.That(judge, Is.Not.Null, "Expected a reviewer_chain entry with role='semantic_judge'.");
        Assert.That(judge!.Agent, Is.EqualTo("ga.CriticAgent"));
        Assert.That(judge.Score, Is.EqualTo(0.88).Within(0.001));
    }

    [Test]
    public async Task ProcessAsync_WithCoordinator_SemanticBasinEvidenceAdded()
    {
        var coordinator = new Mock<IAgentCoordinator>();
        coordinator
            .Setup(c => c.DelegateAsync(It.IsAny<string>(), AgentIds.Critic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse
            {
                AgentId = AgentIds.Critic,
                Result = "Looks consistent.",
                Confidence = 0.75f
            });

        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance)
        {
            Coordinator = coordinator.Object
        };

        var response = await agent.ProcessAsync(new AgentRequest { Query = "changed files: EmbeddingSchema.cs" });
        var verdict = (QaVerdict)response.Data!;

        var basinEvidence = verdict.Evidence.FirstOrDefault(e => e.Kind == "semantic_basin");
        Assert.That(basinEvidence, Is.Not.Null, "Expected a semantic_basin evidence item from CriticAgent.");
        Assert.That(basinEvidence!.Score, Is.EqualTo(0.75).Within(0.001));
    }

    [Test]
    public async Task ProcessAsync_CoordinatorThrows_FallsBackToSkeletonVerdict()
    {
        var coordinator = new Mock<IAgentCoordinator>();
        coordinator
            .Setup(c => c.DelegateAsync(It.IsAny<string>(), AgentIds.Critic, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CriticAgent unavailable"));

        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance)
        {
            Coordinator = coordinator.Object
        };

        var response = await agent.ProcessAsync(new AgentRequest { Query = "diff context" });
        var verdict = (QaVerdict)response.Data!;

        // Should still produce a valid verdict without semantic_judge.
        Assert.That(verdict.SchemaVersion, Is.EqualTo(1));
        Assert.That(verdict.ReviewerChain, Has.None.Matches<QaReviewerEntry>(r => r.Role == "semantic_judge"),
            "On CriticAgent failure, semantic_judge should not appear in the chain.");
    }

    [Test]
    public async Task ProcessAsync_EmptyQuery_WithCoordinator_SkipsCriticAgent()
    {
        var coordinator = new Mock<IAgentCoordinator>();

        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance)
        {
            Coordinator = coordinator.Object
        };

        await agent.ProcessAsync(new AgentRequest { Query = "" });

        // CriticAgent should not be called when Query is empty — no diff to review.
        coordinator.Verify(
            c => c.DelegateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "CriticAgent must not be invoked when the request Query is empty.");
    }

    // ── Verdict structural validity ──────────────────────────────────────────

    [Test]
    public async Task ProcessAsync_WithCoordinator_VerdictIdRemainsFilenamesafe()
    {
        var coordinator = new Mock<IAgentCoordinator>();
        coordinator
            .Setup(c => c.DelegateAsync(It.IsAny<string>(), AgentIds.Critic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse
            {
                AgentId = AgentIds.Critic,
                Result = "Ok",
                Confidence = 0.9f
            });

        var agent = new QAArchitectAgent(_chatClient.Object, NullLogger<QAArchitectAgent>.Instance)
        {
            Coordinator = coordinator.Object
        };
        var response = await agent.ProcessAsync(new AgentRequest { Query = "some diff" });
        var verdict = (QaVerdict)response.Data!;

        Assert.That(verdict.VerdictId, Does.Not.Contain(":"));
        Assert.That(verdict.VerdictId, Does.Not.Contain("/"));
        Assert.That(verdict.VerdictId, Does.Not.Contain("\\"));
    }
}
