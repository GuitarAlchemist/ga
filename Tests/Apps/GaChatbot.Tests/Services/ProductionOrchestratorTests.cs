namespace GaChatbot.Tests.Services;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using GA.Business.ML.Tabs;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Extensions;
using GA.Business.ML.Musical.Explanation;
using GaChatbot.Services;
using GaChatbot.Models;
using GaChatbot.Abstractions;
using GA.Business.ML.Abstractions;
using GA.Business.Core.Abstractions;
using GA.Business.Core.AI;
using NUnit.Framework;
using Moq;
using GaChatbot.Tests.Integration;
using GaChatbot.Tests.Mocks;

[TestFixture]
public class ProductionOrchestratorTests
{
    private ServiceProvider _provider = null!;
    private ProductionOrchestrator _orchestrator = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddGuitarAlchemistAI();
        services.AddSingleton<IVectorIndex>(new MockVectorIndex());
        services.AddSingleton<GA.Business.ML.Musical.Enrichment.ModalFlavorService>();
        services.AddSingleton<VoicingExplanationService>();
        services.AddSingleton<TabPresentationService>();
        services.AddSingleton<GA.Business.ML.Tabs.AdvancedTabSolver>();
        services.AddSingleton<SpectralRetrievalService>();
        services.AddSingleton<SpectralRagOrchestrator>();
        services.AddSingleton<TabAwareOrchestrator>();
        services.AddSingleton<ProductionOrchestrator>();

        // Mock requirements for Orchestrator
        services.AddSingleton(GA.Business.Core.Fretboard.Tuning.Default);
        services.AddSingleton<GA.Business.Core.Fretboard.Analysis.FretboardPositionMapper>();
        services.AddSingleton<GA.Business.Core.Fretboard.Analysis.PhysicalCostService>();
        services.AddSingleton<GroundedPromptBuilder>();
        services.AddSingleton<ResponseValidator>();
        services.AddSingleton<ITextEmbeddingService>(new Mock<ITextEmbeddingService>().Object);
        services.AddSingleton<IEmbeddingGenerator, MusicalEmbeddingGenerator>();
        services.AddSingleton<IGroundedNarrator, MockGroundedNarrator>();

        _provider = services.BuildServiceProvider();
        _orchestrator = _provider.GetRequiredService<ProductionOrchestrator>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider?.Dispose();
    }

    [Test]
    public async Task AnswerAsync_WithValidTab_ReturnsDeepAnalysis()
    {
        // Arrange: Jazz ii-V-I + extra chord to satisfy 4-chord min for Style Classifier
        var tab = @"
e|--5---3---3---3--|
B|--6---3---5---5--|
G|--5---4---4---4--|
D|--7---3---5---5--|
A|--5---5---3---3--|
E|------3----------|";

        var request = new ChatRequest(tab);

        // Act
        var response = await _orchestrator.AnswerAsync(request);

        // Assert
        Assert.That(response.NaturalLanguageAnswer, Does.Contain("harmonic events"));
        Assert.That(response.NaturalLanguageAnswer, Does.Contain("Style Prediction"));
        Assert.That(response.NaturalLanguageAnswer, Does.Contain("G"), "Should identify a key center (G center for ii-V-I barycenter)");
    }

    [Test]
    public async Task AnswerAsync_WithKnowledgeQuery_DelegatesToTabAware()
    {
        // Arrange
        var request = new ChatRequest("What is a Dm9 chord?");

        // Act
        var response = await _orchestrator.AnswerAsync(request);

        // Assert
        // Standard RAG response - without seeds, it falls back
        Assert.That(response.DebugParams?.ToString(), Contains.Substring("Fallback"));
    }
}
