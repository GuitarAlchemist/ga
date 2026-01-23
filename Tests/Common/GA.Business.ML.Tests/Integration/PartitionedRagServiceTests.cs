namespace GA.Business.ML.Tests.Integration;

using System.Collections.Generic;
using System.Threading.Tasks;
using GA.Business.ML.Rag;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using GA.Data.MongoDB.Services.DocumentServices.Rag;
using GA.Data.MongoDB.Models.Rag;

[TestFixture]
public class PartitionedRagServiceTests
{
    private PartitionedRagService _service;
    private Mock<MusicTheoryRagService> _theoryMock;
    private Mock<GuitarTechniqueRagService> _techMock;
    private Mock<StyleLearningRagService> _styleMock;
    private Mock<YouTubeTranscriptRagService> _youtubeMock;
    private Mock<EnhancedChordRagService> _chordMock;

    [SetUp]
    public void Setup()
    {
        // Use Moq to create mocks of the services
        _theoryMock = new Mock<MusicTheoryRagService>(
            NullLogger<MusicTheoryRagService>.Instance, null!, null!);
        _techMock = new Mock<GuitarTechniqueRagService>(
            NullLogger<GuitarTechniqueRagService>.Instance, null!, null!);
        _styleMock = new Mock<StyleLearningRagService>(
            NullLogger<StyleLearningRagService>.Instance, null!, null!);
        _youtubeMock = new Mock<YouTubeTranscriptRagService>(
            NullLogger<YouTubeTranscriptRagService>.Instance, null!, null!);
        _chordMock = new Mock<EnhancedChordRagService>(
            NullLogger<EnhancedChordRagService>.Instance, null!, null!);

        _service = new PartitionedRagService(
            NullLogger<PartitionedRagService>.Instance,
            _theoryMock.Object,
            _techMock.Object,
            _styleMock.Object,
            _youtubeMock.Object,
            _chordMock.Object);
    }

    [Test]
    public void ParseStructuredQuery_ExtractsChordsAndScales()
    {
        var query = "How to play Cmaj7 extensions with G Mixolydian?";
        var structured = _service.ParseStructuredQuery(query);

        Assert.That(structured.Chords, Contains.Item("Cmaj7"));
        Assert.That(structured.Scales, Contains.Item("G Mixolydian"));
        Assert.That(structured.RecommendedPartitions, Contains.Item(KnowledgeType.Theory));
    }

    [Test]
    public void ParseStructuredQuery_DetectsTechniques()
    {
        var query = "Sweep picking exercises for fusion";
        var structured = _service.ParseStructuredQuery(query);

        Assert.That(structured.Techniques, Contains.Item("Sweep"));
        Assert.That(structured.RecommendedPartitions, Contains.Item(KnowledgeType.Technique));
    }

    [Test]
    public async Task QueryAsync_CallsCorrectPartitions()
    {
        // Mock a simple return
        _theoryMock.Setup(m => m.SearchWithScoresAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>()))
            .ReturnsAsync(new List<(MusicTheoryRagDocument Document, float Score)>());

        var result = await _service.QueryAsync("Test", new[] { KnowledgeType.Theory }, 5);

        Assert.That(result, Is.Not.Null);
        _theoryMock.Verify(m => m.SearchWithScoresAsync("Test", 5, 0.0), Times.Once);
    }

    [Test]
    public async Task RunBenchmark_EvaluatesMultipleQueries()
    {
        var evalService = new RagEvaluationService(_service, NullLogger<RagEvaluationService>.Instance);
        var testCases = new[]
        {
            new RagTestCase("Cmaj7 playability", new[] { KnowledgeType.Technique }, new[] { "finger", "position" }),
            new RagTestCase("Atonal set theory", new[] { KnowledgeType.Theory }, new[] { "pitch", "set" })
        };

        // Mock responses for both cases
        _techMock.Setup(m => m.SearchWithScoresAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>()))
            .ReturnsAsync(new List<(GuitarTechniqueRagDocument Document, float Score)> 
            { 
                (new GuitarTechniqueRagDocument { Content = "Use fingers 1, 2, 3" }, 0.9f) 
            });
        
        _theoryMock.Setup(m => m.SearchWithScoresAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>()))
            .ReturnsAsync(new List<(MusicTheoryRagDocument Document, float Score)> 
            { 
                (new MusicTheoryRagDocument { Content = "Pitch class set analysis" }, 0.85f) 
            });

        var result = await evalService.RunBenchmarkAsync("Unit Test Benchmark", testCases);

        Assert.That(result.TotalQueries, Is.EqualTo(2));
        Assert.That(result.AverageResultsPerQuery, Is.GreaterThan(0));
    }
}
