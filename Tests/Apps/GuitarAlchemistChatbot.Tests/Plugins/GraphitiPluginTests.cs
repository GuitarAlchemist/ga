namespace GuitarAlchemistChatbot.Tests.Plugins;

using GuitarAlchemistChatbot.Plugins;
using GuitarAlchemistChatbot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

/// <summary>
///     Integration tests for GraphitiPlugin
///     Tests temporal knowledge graph functionality via AI plugin
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Plugins")]
[Category("Graphiti")]
public class GraphitiPluginTests
{
    [SetUp]
    public void Setup()
    {
        _graphitiClientMock = new Mock<GraphitiClient>(
            Mock.Of<HttpClient>(),
            Mock.Of<ILogger<GraphitiClient>>());

        _loggerMock = new Mock<ILogger<GraphitiPlugin>>();
        _plugin = new GraphitiPlugin(_graphitiClientMock.Object, _loggerMock.Object);
    }

    private Mock<GraphitiClient>? _graphitiClientMock;
    private Mock<ILogger<GraphitiPlugin>>? _loggerMock;
    private GraphitiPlugin? _plugin;

    [Test]
    public async Task SearchKnowledgeGraphAsync_WithValidQuery_ReturnsFormattedResults()
    {
        // Arrange
        var query = "C major chord voicings";
        var mockResponse = new GraphitiSearchResponse(
            "success",
            query,
            new List<GraphitiSearchResult>
            {
                new("C Major Open Position - Root on 5th string", 0.95, "ChordVoicing"),
                new("C Major Barre Chord - Movable shape", 0.88, "ChordVoicing")
            },
            2);

        _graphitiClientMock!
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.SearchKnowledgeGraphAsync(query);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Knowledge Graph Search Results"));
        Assert.That(result, Does.Contain("C Major Open Position"));
        Assert.That(result, Does.Contain("0.95"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task SearchKnowledgeGraphAsync_WithNoResults_ReturnsNoResultsMessage()
    {
        // Arrange
        var mockResponse = new GraphitiSearchResponse(
            "success",
            "nonexistent query",
            new List<GraphitiSearchResult>(),
            0);

        _graphitiClientMock!
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.SearchKnowledgeGraphAsync("nonexistent query");

        // Assert
        Assert.That(result, Does.Contain("No results found"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task SearchKnowledgeGraphAsync_WhenApiReturnsNull_ReturnsNoResultsMessage()
    {
        // Arrange
        _graphitiClientMock!
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GraphitiSearchResponse?)null);

        // Act
        var result = await _plugin!.SearchKnowledgeGraphAsync("test query");

        // Assert
        Assert.That(result, Does.Contain("No results found"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GetRecommendationsAsync_WithValidRecommendationType_ReturnsFormattedRecommendations()
    {
        // Arrange
        var recommendationType = "next_chord";
        var mockResponse = new GraphitiRecommendationResponse(
            "success",
            "chatbot-user",
            recommendationType,
            new List<GraphitiRecommendation>
            {
                new(
                    "ChordProgression",
                    "Jazz ii-V-I Progression",
                    0.92,
                    "Based on your interest in jazz harmony"),
                new(
                    "Scale",
                    "Dorian Mode",
                    0.85,
                    "Complements your recent practice")
            });

        _graphitiClientMock!
            .Setup(x => x.GetRecommendationsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GetRecommendationsAsync(recommendationType);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Personalized Recommendations"));
        Assert.That(result, Does.Contain("Jazz ii-V-I Progression"));
        Assert.That(result, Does.Contain("Dorian Mode"));
        Assert.That(result, Does.Contain("0.92"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GetRecommendationsAsync_WithNoRecommendations_ReturnsNoRecommendationsMessage()
    {
        // Arrange
        var mockResponse = new GraphitiRecommendationResponse(
            "success",
            "chatbot-user",
            "next_chord",
            new List<GraphitiRecommendation>());

        _graphitiClientMock!
            .Setup(x => x.GetRecommendationsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GetRecommendationsAsync();

        // Assert
        Assert.That(result, Does.Contain("No recommendations available"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GetLearningProgressAsync_WithValidProgress_ReturnsFormattedProgress()
    {
        // Arrange
        var mockResponse = new GraphitiUserProgressResponse(
            "success",
            "chatbot-user",
            new GraphitiUserProgress(
                5.5,
                45,
                "Practiced jazz voicings",
                "Improving steadily",
                "Master modal harmony"));

        _graphitiClientMock!
            .Setup(x => x.GetUserProgressAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GetLearningProgressAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Learning Progress"));
        Assert.That(result, Does.Contain("5.50"));
        Assert.That(result, Does.Contain("45"));
        Assert.That(result, Does.Contain("Practiced jazz voicings"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GetLearningProgressAsync_WhenApiReturnsNull_ReturnsNoDataMessage()
    {
        // Arrange
        _graphitiClientMock!
            .Setup(x => x.GetUserProgressAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GraphitiUserProgressResponse?)null);

        // Act
        var result = await _plugin!.GetLearningProgressAsync();

        // Assert
        Assert.That(result, Does.Contain("No learning progress data available"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task RecordLearningEpisodeAsync_WithValidData_ReturnsSuccessMessage()
    {
        // Arrange
        var episodeType = "practice_session";
        var description = "Practiced jazz voicings";

        var mockResponse = new GraphitiResponse(
            "success",
            "Episode recorded successfully",
            null);

        _graphitiClientMock!
            .Setup(x => x.AddEpisodeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.RecordLearningEpisodeAsync(episodeType, description);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Learning Episode Recorded"));
        Assert.That(result, Does.Contain("practice_session"));
        Assert.That(result, Does.Contain("Practiced jazz voicings"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task RecordLearningEpisodeAsync_WithFailure_ReturnsFailureMessage()
    {
        // Arrange
        var mockResponse = new GraphitiResponse(
            "error",
            "Failed to record episode",
            null);

        _graphitiClientMock!
            .Setup(x => x.AddEpisodeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.RecordLearningEpisodeAsync("practice_session", "activity");

        // Assert
        Assert.That(result, Does.Contain("Failed to record"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task RecordLearningEpisodeAsync_WhenApiReturnsNull_ReturnsErrorMessage()
    {
        // Arrange
        _graphitiClientMock!
            .Setup(x => x.AddEpisodeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GraphitiResponse?)null);

        // Act
        var result = await _plugin!.RecordLearningEpisodeAsync("practice_session", "activity");

        // Assert
        Assert.That(result, Does.Contain("Failed to record"));

        TestContext.WriteLine(result);
    }
}
