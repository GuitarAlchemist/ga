namespace GuitarAlchemistChatbot.Tests.Plugins;

using GuitarAlchemistChatbot.Plugins;
using GuitarAlchemistChatbot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

/// <summary>
///     Integration tests for ShapeGraphPlugin
///     Tests comprehensive harmonic analysis functionality via AI plugin
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Plugins")]
[Category("ShapeGraph")]
public class ShapeGraphPluginTests
{
    [SetUp]
    public void Setup()
    {
        _gaApiClientMock = new Mock<GaApiClient>(
            Mock.Of<HttpClient>(),
            Mock.Of<ILogger<GaApiClient>>());

        _loggerMock = new Mock<ILogger<ShapeGraphPlugin>>();
        _plugin = new ShapeGraphPlugin(_gaApiClientMock.Object, _loggerMock.Object);
    }

    private Mock<GaApiClient>? _gaApiClientMock;
    private Mock<ILogger<ShapeGraphPlugin>>? _loggerMock;
    private ShapeGraphPlugin? _plugin;

    [Test]
    public async Task AnalyzeShapeGraphAsync_WithValidParameters_ReturnsFormattedAnalysis()
    {
        // Arrange
        var pitchClasses = "0,4,7"; // C major triad
        var tuning = "E2 A2 D3 G3 B3 E4";

        var mockResponse = new ShapeGraphAnalysisResponse(
            new SpectralMetricsDto(
                0.85,
                0.42,
                1,
                2.5,
                5),
            new List<ChordFamilyDto>
            {
                new(1, new List<string> { "shape1", "shape2" }, "shape1")
            },
            new List<CentralShapeDto>(),
            new List<BottleneckDto>(),
            new DynamicsDto(
                new List<AttractorDto>
                {
                    new("shape1", 5.0, "stable")
                },
                new List<LimitCycleDto>
                {
                    new(new List<string> { "shape1", "shape2" }, 2, 0.88)
                },
                0.15,
                false,
                true),
            null);

        _gaApiClientMock!
            .Setup(x => x.AnalyzeShapeGraphAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeShapeGraphAsync(pitchClasses, tuning);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Shape Graph Analysis"));
        Assert.That(result, Does.Contain("Spectral Metrics"));
        Assert.That(result, Does.Contain("0.85")); // Algebraic connectivity
        Assert.That(result, Does.Contain("Chord Families"));
        Assert.That(result, Does.Contain("Attractors"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeShapeGraphAsync_WithHighAlgebraicConnectivity_ShowsWellConnected()
    {
        // Arrange
        var mockResponse = new ShapeGraphAnalysisResponse(
            new SpectralMetricsDto(
                0.95, // High connectivity
                0.5,
                1,
                2.0,
                4),
            new List<ChordFamilyDto>(),
            new List<CentralShapeDto>(),
            new List<BottleneckDto>(),
            new DynamicsDto(
                new List<AttractorDto>(),
                new List<LimitCycleDto>(),
                0.1,
                false,
                true),
            null);

        _gaApiClientMock!
            .Setup(x => x.AnalyzeShapeGraphAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeShapeGraphAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("well-connected"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeShapeGraphAsync_WithLowAlgebraicConnectivity_ShowsFragmented()
    {
        // Arrange
        var mockResponse = new ShapeGraphAnalysisResponse(
            new SpectralMetricsDto(
                0.25, // Low connectivity
                0.15,
                3,
                4.5,
                8),
            new List<ChordFamilyDto>(),
            new List<CentralShapeDto>(),
            new List<BottleneckDto>(),
            new DynamicsDto(
                new List<AttractorDto>(),
                new List<LimitCycleDto>(),
                0.05,
                false,
                true),
            null);

        _gaApiClientMock!
            .Setup(x => x.AnalyzeShapeGraphAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeShapeGraphAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("fragmented"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeShapeGraphAsync_WithAttractors_ShowsAttractorInformation()
    {
        // Arrange
        var mockResponse = new ShapeGraphAnalysisResponse(
            new SpectralMetricsDto(
                0.8,
                0.4,
                1,
                2.5,
                5),
            new List<ChordFamilyDto>(),
            new List<CentralShapeDto>(),
            new List<BottleneckDto>(),
            new DynamicsDto(
                new List<AttractorDto>
                {
                    new("shape1", 8.0, "stable"),
                    new("shape2", 5.0, "stable")
                },
                new List<LimitCycleDto>(),
                0.12,
                false,
                true),
            null);

        _gaApiClientMock!
            .Setup(x => x.AnalyzeShapeGraphAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeShapeGraphAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Attractors"));
        Assert.That(result, Does.Contain("shape1"));
        Assert.That(result, Does.Contain("basin: 8"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeShapeGraphAsync_WithLimitCycles_ShowsCycleInformation()
    {
        // Arrange
        var mockResponse = new ShapeGraphAnalysisResponse(
            new SpectralMetricsDto(
                0.8,
                0.4,
                1,
                2.5,
                5),
            new List<ChordFamilyDto>(),
            new List<CentralShapeDto>(),
            new List<BottleneckDto>(),
            new DynamicsDto(
                new List<AttractorDto>(),
                new List<LimitCycleDto>
                {
                    new(new List<string> { "shape1", "shape2", "shape3" }, 3, 0.92)
                },
                0.18,
                false,
                true),
            null);

        _gaApiClientMock!
            .Setup(x => x.AnalyzeShapeGraphAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeShapeGraphAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Limit Cycles"));
        Assert.That(result, Does.Contain("period: 3"));
        Assert.That(result, Does.Contain("stability: 0.92"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeShapeGraphAsync_WithPositiveLyapunov_ShowsChaotic()
    {
        // Arrange
        var mockResponse = new ShapeGraphAnalysisResponse(
            new SpectralMetricsDto(
                0.7,
                0.35,
                1,
                2.5,
                5),
            new List<ChordFamilyDto>(),
            new List<CentralShapeDto>(),
            new List<BottleneckDto>(),
            new DynamicsDto(
                new List<AttractorDto>(),
                new List<LimitCycleDto>(),
                0.25,
                true,
                false),
            null);

        _gaApiClientMock!
            .Setup(x => x.AnalyzeShapeGraphAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.AnalyzeShapeGraphAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("chaotic"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeShapeGraphAsync_WhenApiReturnsNull_ReturnsErrorMessage()
    {
        // Arrange
        _gaApiClientMock!
            .Setup(x => x.AnalyzeShapeGraphAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShapeGraphAnalysisResponse?)null);

        // Act
        var result = await _plugin!.AnalyzeShapeGraphAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Error"));
        Assert.That(result, Does.Contain("Failed to analyze"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task AnalyzeShapeGraphAsync_WithInvalidPitchClasses_ReturnsErrorMessage()
    {
        // Arrange
        var invalidPitchClasses = "invalid,data,here";

        // Act
        var result = await _plugin!.AnalyzeShapeGraphAsync(invalidPitchClasses);

        // Assert
        Assert.That(result, Does.Contain("Error"));
        Assert.That(result, Does.Contain("No valid pitch classes"));

        TestContext.WriteLine(result);
    }
}
