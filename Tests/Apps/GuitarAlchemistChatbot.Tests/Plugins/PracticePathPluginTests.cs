namespace GuitarAlchemistChatbot.Tests.Plugins;

using GuitarAlchemistChatbot.Plugins;
using GuitarAlchemistChatbot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

/// <summary>
///     Integration tests for PracticePathPlugin
///     Tests practice path generation functionality via AI plugin
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Plugins")]
[Category("PracticePath")]
public class PracticePathPluginTests
{
    [SetUp]
    public void Setup()
    {
        _gaApiClientMock = new Mock<GaApiClient>(
            Mock.Of<HttpClient>(),
            Mock.Of<ILogger<GaApiClient>>());

        _loggerMock = new Mock<ILogger<PracticePathPlugin>>();
        _plugin = new PracticePathPlugin(_gaApiClientMock.Object, _loggerMock.Object);
    }

    private Mock<GaApiClient>? _gaApiClientMock;
    private Mock<ILogger<PracticePathPlugin>>? _loggerMock;
    private PracticePathPlugin? _plugin;

    [Test]
    public async Task GeneratePracticePathAsync_WithValidParameters_ReturnsFormattedPath()
    {
        // Arrange
        var pitchClasses = "0,4,7"; // C major triad
        var tuning = "E2 A2 D3 G3 B3 E4";
        var pathLength = 10;
        var strategy = "balanced";

        var mockResponse = new OptimizedPracticePathResponse(
            new List<string> { "shape1", "shape2", "shape3" },
            new List<FretboardShapeResponse>
            {
                new("shape1", new[] { new PositionResponse(1, 3, false) }, 3, 5, 3, 0.8, 0.85, 2, new[] { "tag1" }),
                new("shape2", new[] { new PositionResponse(2, 4, false) }, 4, 6, 3, 0.7, 0.80, 2, new[] { "tag2" }),
                new("shape3", new[] { new PositionResponse(3, 5, false) }, 5, 7, 3, 0.6, 0.75, 2, new[] { "tag3" })
            },
            2.5,
            0.65,
            0.75,
            0.80,
            0.85);

        _gaApiClientMock!
            .Setup(x => x.GeneratePracticePathAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GeneratePracticePathAsync(pitchClasses, tuning, pathLength, strategy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Optimal Practice Path Generated"));
        Assert.That(result, Does.Contain("0.85")); // Quality
        Assert.That(result, Does.Contain("0.80")); // Diversity
        Assert.That(result, Does.Contain("balanced"));

        TestContext.WriteLine(result);
    }

    [Test]
    [TestCase("MaximizeInformationGain")]
    [TestCase("MinimizeVoiceLeading")]
    [TestCase("ExploreFamilies")]
    [TestCase("FollowAttractors")]
    [TestCase("Balanced")]
    public async Task GeneratePracticePathAsync_WithDifferentStrategies_ReturnsValidPaths(string strategy)
    {
        // Arrange
        var pitchClasses = "0,4,7";
        var tuning = "E2 A2 D3 G3 B3 E4";

        var mockResponse = new OptimizedPracticePathResponse(
            new List<string> { "shape1", "shape2" },
            new List<FretboardShapeResponse>
            {
                new("shape1", new[] { new PositionResponse(1, 3, false) }, 3, 5, 3, 0.8, 0.85, 2, new[] { "tag1" })
            },
            2.0,
            0.5,
            0.7,
            0.75,
            0.80);

        _gaApiClientMock!
            .Setup(x => x.GeneratePracticePathAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GeneratePracticePathAsync(pitchClasses, tuning, 8, strategy);

        // Assert
        Assert.That(result, Does.Contain("Optimal Practice Path Generated"));
        Assert.That(result, Does.Contain(strategy));

        TestContext.WriteLine($"Strategy: {strategy}");
        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GeneratePracticePathAsync_WithHighQuality_ShowsExcellentRecommendation()
    {
        // Arrange
        var mockResponse = new OptimizedPracticePathResponse(
            new List<string> { "shape1" },
            new List<FretboardShapeResponse>
            {
                new("shape1", new[] { new PositionResponse(1, 3, false) }, 3, 5, 3, 0.8, 0.85, 2, new[] { "tag1" })
            },
            2.0,
            0.5,
            0.7,
            0.85,
            0.90); // High quality

        _gaApiClientMock!
            .Setup(x => x.GeneratePracticePathAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GeneratePracticePathAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Excellent path"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GeneratePracticePathAsync_WithLowQuality_ShowsNeedsImprovementRecommendation()
    {
        // Arrange
        var mockResponse = new OptimizedPracticePathResponse(
            new List<string> { "shape1" },
            new List<FretboardShapeResponse>
            {
                new("shape1", new[] { new PositionResponse(1, 3, false) }, 3, 5, 3, 0.8, 0.85, 2, new[] { "tag1" })
            },
            2.0,
            0.5,
            0.7,
            0.30,
            0.40); // Low quality

        _gaApiClientMock!
            .Setup(x => x.GeneratePracticePathAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GeneratePracticePathAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Needs improvement"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GeneratePracticePathAsync_WithHighDiversity_ShowsHighDiversityRecommendation()
    {
        // Arrange
        var mockResponse = new OptimizedPracticePathResponse(
            new List<string> { "shape1", "shape2", "shape3" },
            new List<FretboardShapeResponse>
            {
                new("shape1", new[] { new PositionResponse(1, 3, false) }, 3, 5, 3, 0.8, 0.85, 2, new[] { "tag1" }),
                new("shape2", new[] { new PositionResponse(2, 8, false) }, 8, 10, 3, 0.7, 0.80, 2, new[] { "tag2" }),
                new("shape3", new[] { new PositionResponse(3, 12, false) }, 12, 14, 3, 0.6, 0.75, 2, new[] { "tag3" })
            },
            2.5,
            0.65,
            0.75,
            0.90, // High diversity
            0.85);

        _gaApiClientMock!
            .Setup(x => x.GeneratePracticePathAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GeneratePracticePathAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("High diversity"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GeneratePracticePathAsync_WhenApiReturnsNull_ReturnsErrorMessage()
    {
        // Arrange
        _gaApiClientMock!
            .Setup(x => x.GeneratePracticePathAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OptimizedPracticePathResponse?)null);

        // Act
        var result = await _plugin!.GeneratePracticePathAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Error"));
        Assert.That(result, Does.Contain("Failed to generate"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GeneratePracticePathAsync_WithInvalidPitchClasses_ReturnsErrorMessage()
    {
        // Arrange
        var invalidPitchClasses = "invalid,data,here";

        // Act
        var result = await _plugin!.GeneratePracticePathAsync(invalidPitchClasses);

        // Assert
        Assert.That(result, Does.Contain("Error"));
        Assert.That(result, Does.Contain("No valid pitch classes"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GeneratePracticePathAsync_WithShapeDetails_IncludesShapeInformation()
    {
        // Arrange
        var mockResponse = new OptimizedPracticePathResponse(
            new List<string> { "shape1", "shape2" },
            new List<FretboardShapeResponse>
            {
                new("shape1",
                    new[] { new PositionResponse(1, 3, false), new PositionResponse(2, 5, false) },
                    3, 5, 3, 0.8, 0.85, 2, new[] { "tag1" }),
                new("shape2",
                    new[] { new PositionResponse(1, 4, false), new PositionResponse(2, 6, false) },
                    4, 6, 3, 0.7, 0.80, 2, new[] { "tag2" })
            },
            2.0,
            0.5,
            0.7,
            0.75,
            0.80);

        _gaApiClientMock!
            .Setup(x => x.GeneratePracticePathAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GeneratePracticePathAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("shape1"));
        Assert.That(result, Does.Contain("span:"));
        Assert.That(result, Does.Contain("ergonomics:"));
        Assert.That(result, Does.Contain("Positions:"));

        TestContext.WriteLine(result);
    }
}
