namespace GuitarAlchemistChatbot.Tests.Plugins;

using GuitarAlchemistChatbot.Plugins;
using GuitarAlchemistChatbot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

/// <summary>
///     Integration tests for ChordProgressionPlugin
///     Tests chord progression analysis functionality via AI plugin
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Plugins")]
[Category("ChordProgression")]
public class ChordProgressionPluginTests
{
    [SetUp]
    public void Setup()
    {
        _gaApiClientMock = new Mock<GaApiClient>(
            Mock.Of<HttpClient>(),
            Mock.Of<ILogger<GaApiClient>>());

        _loggerMock = new Mock<ILogger<ChordProgressionPlugin>>();
        _plugin = new ChordProgressionPlugin(_gaApiClientMock.Object, _loggerMock.Object);
    }

    private Mock<GaApiClient>? _gaApiClientMock;
    private Mock<ILogger<ChordProgressionPlugin>>? _loggerMock;
    private ChordProgressionPlugin? _plugin;

    [Test]
    public async Task AnalyzeProgressionAsync_WithValidProgression_ReturnsFormattedAnalysis()
    {
        // Arrange
        var pitchClassSets = "0,4,7 | 5,9,0 | 7,11,2 | 0,4,7"; // I-IV-V-I

        var mockResponse = new ProgressionAnalysisResponse(
            2.5,
            0.65,
            0.85,
            3,
            4,
            new List<NextShapeSuggestion>
            {
                new("shape1", 1.2, 0.45),
                new("shape2", 0.8, 0.25)
            });

        _gaApiClientMock!
            .Setup(x => x.AnalyzeProgressionAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Chord Progression Analysis"));
        Assert.That(result, Does.Contain("2.5")); // Entropy
        Assert.That(result, Does.Contain("0.65")); // Complexity
        Assert.That(result, Does.Contain("0.85")); // Predictability
        Assert.That(result, Does.Contain("Moderate entropy"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WithLowEntropy_ShowsLowEntropyInterpretation()
    {
        // Arrange
        var pitchClassSets = "0,4,7 | 0,4,7 | 0,4,7";

        var mockResponse = new ProgressionAnalysisResponse(
            0.5,
            0.1,
            0.95,
            1,
            3,
            new List<NextShapeSuggestion>());

        _gaApiClientMock!
            .Setup(x => x.AnalyzeProgressionAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert
        Assert.That(result, Does.Contain("Low entropy"));
        Assert.That(result, Does.Contain("predictable"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WithHighEntropy_ShowsHighEntropyInterpretation()
    {
        // Arrange
        var pitchClassSets = "0,4,7 | 1,5,8 | 3,7,10 | 6,10,1";

        var mockResponse = new ProgressionAnalysisResponse(
            3.5,
            0.85,
            0.25,
            4,
            4,
            new List<NextShapeSuggestion>());

        _gaApiClientMock!
            .Setup(x => x.AnalyzeProgressionAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert
        Assert.That(result, Does.Contain("High entropy"));
        Assert.That(result, Does.Contain("unpredictable"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WithSuggestions_IncludesSuggestions()
    {
        // Arrange
        var pitchClassSets = "0,4,7 | 5,9,0";

        var mockResponse = new ProgressionAnalysisResponse(
            2.0,
            0.5,
            0.7,
            2,
            2,
            new List<NextShapeSuggestion>
            {
                new("Cmaj7", 1.5, 0.5),
                new("Am7", 1.2, 0.3),
                new("Dm7", 0.9, 0.2)
            });

        _gaApiClientMock!
            .Setup(x => x.AnalyzeProgressionAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert
        Assert.That(result, Does.Contain("Next Chord Suggestions"));
        Assert.That(result, Does.Contain("Cmaj7"));
        Assert.That(result, Does.Contain("1.5")); // Info gain
        Assert.That(result, Does.Contain("0.5")); // Probability

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WhenApiReturnsNull_ReturnsErrorMessage()
    {
        // Arrange
        var pitchClassSets = "invalid";

        _gaApiClientMock!
            .Setup(x => x.AnalyzeProgressionAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProgressionAnalysisResponse?)null);

        // Act
        var result = await _plugin!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert
        Assert.That(result, Does.Contain("Error"));
        Assert.That(result, Does.Contain("Failed to analyze"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WithDifferentSeparators_ParsesCorrectly()
    {
        // Arrange - Test different separators
        var testCases = new[]
        {
            "0,4,7 | 5,9,0", // Pipe separator
            "0,4,7 -> 5,9,0", // Arrow separator
            "0,4,7 5,9,0", // Space separator
            "0,4,7|5,9,0" // No spaces
        };

        var mockResponse = new ProgressionAnalysisResponse(
            2.0,
            0.5,
            0.7,
            2,
            2,
            new List<NextShapeSuggestion>());

        _gaApiClientMock!
            .Setup(x => x.AnalyzeProgressionAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act & Assert
        foreach (var testCase in testCases)
        {
            var result = await _plugin!.AnalyzeProgressionAsync(testCase);
            Assert.That(result, Does.Contain("Chord Progression Analysis"),
                $"Should parse: {testCase}");
            TestContext.WriteLine($"Parsed: {testCase}");
        }
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WithComplexityLevels_ShowsCorrectInterpretation()
    {
        // Arrange - Test different complexity levels
        var testCases = new[]
        {
            (Complexity: 0.2, Expected: "Low complexity"),
            (Complexity: 0.5, Expected: "Moderate complexity"),
            (Complexity: 0.8, Expected: "High complexity")
        };

        foreach (var (complexity, expected) in testCases)
        {
            var mockResponse = new ProgressionAnalysisResponse(
                2.0,
                complexity,
                0.7,
                2,
                2,
                new List<NextShapeSuggestion>());

            _gaApiClientMock!
                .Setup(x => x.AnalyzeProgressionAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _plugin!.AnalyzeProgressionAsync("0,4,7 | 5,9,0");

            // Assert
            Assert.That(result, Does.Contain(expected),
                $"Complexity {complexity} should show '{expected}'");
            TestContext.WriteLine($"Complexity {complexity}: {expected}");
        }
    }
}
