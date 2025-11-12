namespace GuitarAlchemistChatbot.Tests.Plugins;

using GuitarAlchemistChatbot.Plugins;
using GuitarAlchemistChatbot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

/// <summary>
///     Integration tests for BSPDungeonPlugin
///     Tests BSP dungeon generation functionality via AI plugin
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Plugins")]
[Category("BSPDungeon")]
public class BspDungeonPluginTests
{
    [SetUp]
    public void Setup()
    {
        _gaApiClientMock = new Mock<GaApiClient>(
            Mock.Of<HttpClient>(),
            Mock.Of<ILogger<GaApiClient>>());

        _loggerMock = new Mock<ILogger<BspDungeonPlugin>>();
        _plugin = new BspDungeonPlugin(_gaApiClientMock.Object, _loggerMock.Object);
    }

    private Mock<GaApiClient>? _gaApiClientMock;
    private Mock<ILogger<BspDungeonPlugin>>? _loggerMock;
    private BspDungeonPlugin? _plugin;

    [Test]
    public async Task GenerateDungeonAsync_WithValidParameters_ReturnsFormattedDungeon()
    {
        // Arrange
        var width = 100;
        var height = 80;
        var maxDepth = 5;
        var seed = 12345;

        var mockResponse = new DungeonGenerationResponse(
            width,
            height,
            seed,
            new List<DungeonRoom>
            {
                new(10, 10, 20, 15, 20, 17),
                new(40, 30, 25, 20, 52, 40)
            },
            new List<DungeonCorridor>
            {
                new(2, new List<DungeonPoint>
                {
                    new(20, 17),
                    new(40, 40)
                })
            });

        _gaApiClientMock!
            .Setup(x => x.GenerateDungeonAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GenerateDungeonAsync(width, height, maxDepth, seed);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("BSP Dungeon Generated"));
        Assert.That(result, Does.Contain("100x80"));
        Assert.That(result, Does.Contain("2 rooms"));
        Assert.That(result, Does.Contain("1 corridors"));
        Assert.That(result, Does.Contain("12345"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GenerateDungeonAsync_WithDefaultParameters_ReturnsFormattedDungeon()
    {
        // Arrange
        var mockResponse = new DungeonGenerationResponse(
            80,
            60,
            null,
            new List<DungeonRoom>
            {
                new(10, 10, 20, 15, 20, 17)
            },
            new List<DungeonCorridor>());

        _gaApiClientMock!
            .Setup(x => x.GenerateDungeonAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GenerateDungeonAsync();

        // Assert
        Assert.That(result, Does.Contain("BSP Dungeon Generated"));
        Assert.That(result, Does.Contain("80x60"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GenerateDungeonAsync_WhenApiReturnsNull_ReturnsErrorMessage()
    {
        // Arrange
        _gaApiClientMock!
            .Setup(x => x.GenerateDungeonAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DungeonGenerationResponse?)null);

        // Act
        var result = await _plugin!.GenerateDungeonAsync();

        // Assert
        Assert.That(result, Does.Contain("Error"));
        Assert.That(result, Does.Contain("Failed to generate"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GenerateIntelligentDungeonAsync_WithValidParameters_ReturnsFormattedDungeon()
    {
        // Arrange
        var pitchClasses = "0,4,7"; // C major triad
        var tuning = "E2 A2 D3 G3 B3 E4";
        var width = 100;
        var height = 80;

        var mockLearningPath = new OptimizedPracticePathResponse(
            new List<string> { "shape1", "shape2" },
            new List<FretboardShapeResponse>(),
            2.3,
            0.68,
            0.6,
            0.7,
            0.8);

        var mockResponse = new IntelligentDungeonResponse(
            width,
            height,
            new List<DungeonFloor>
            {
                new(1, 1, 5, new List<string> { "shape1", "shape2" })
            },
            new List<DungeonLandmark>
            {
                new("Central Hub", "shape1", 10, 10, 0.85),
                new("Practice Area", "shape2", 40, 30, 0.72)
            },
            new List<DungeonPortal>(),
            new List<DungeonSafeZone>
            {
                new("shape1", 10, 10, 8.0)
            },
            new List<DungeonChallengePath>(),
            mockLearningPath);

        _gaApiClientMock!
            .Setup(x => x.GenerateIntelligentDungeonAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GenerateIntelligentDungeonAsync(pitchClasses, tuning, width, height);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Intelligent Musical Dungeon"));
        Assert.That(result, Does.Contain("100x80"));
        Assert.That(result, Does.Contain("shape1"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GenerateIntelligentDungeonAsync_WithDefaultParameters_ReturnsFormattedDungeon()
    {
        // Arrange
        var mockLearningPath = new OptimizedPracticePathResponse(
            new List<string> { "shape1", "shape2" },
            new List<FretboardShapeResponse>(),
            2.0,
            0.5,
            0.7,
            0.6,
            0.8);

        var mockResponse = new IntelligentDungeonResponse(
            80,
            60,
            new List<DungeonFloor>
            {
                new(1, 1, 5, new List<string> { "shape1", "shape2" })
            },
            new List<DungeonLandmark>
            {
                new("Central Hub", "shape1", 40, 30, 0.9)
            },
            new List<DungeonPortal>
            {
                new(1, 2, "shape2", 0.8)
            },
            new List<DungeonSafeZone>
            {
                new("shape1", 40, 30, 8.0)
            },
            new List<DungeonChallengePath>
            {
                new(new List<string> { "shape1", "shape2" }, 2, 0.9)
            },
            mockLearningPath);

        _gaApiClientMock!
            .Setup(x => x.GenerateIntelligentDungeonAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GenerateIntelligentDungeonAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Intelligent Musical Dungeon"));
        Assert.That(result, Does.Contain("80x60"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GenerateIntelligentDungeonAsync_WithHighComplexity_ShowsChallengingMessage()
    {
        // Arrange
        var mockLearningPath = new OptimizedPracticePathResponse(
            new List<string> { "shape1", "shape2", "shape3" },
            new List<FretboardShapeResponse>(),
            3.5,
            0.95,
            0.3,
            0.9,
            0.7);

        var mockResponse = new IntelligentDungeonResponse(
            100,
            80,
            new List<DungeonFloor>
            {
                new(1, 1, 10, new List<string> { "shape1", "shape2", "shape3" }),
                new(2, 2, 8, new List<string> { "shape4", "shape5" })
            },
            new List<DungeonLandmark>
            {
                new("Boss Room", "shape1", 50, 40, 0.95)
            },
            new List<DungeonPortal>
            {
                new(1, 2, "shape2", 0.9)
            },
            new List<DungeonSafeZone>(),
            new List<DungeonChallengePath>
            {
                new(new List<string> { "shape1", "shape2", "shape3" }, 3, 0.95)
            },
            mockLearningPath);

        _gaApiClientMock!
            .Setup(x => x.GenerateIntelligentDungeonAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _plugin!.GenerateIntelligentDungeonAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Very challenging"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GenerateIntelligentDungeonAsync_WhenApiReturnsNull_ReturnsErrorMessage()
    {
        // Arrange
        _gaApiClientMock!
            .Setup(x => x.GenerateIntelligentDungeonAsync(
                It.IsAny<int[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntelligentDungeonResponse?)null);

        // Act
        var result = await _plugin!.GenerateIntelligentDungeonAsync("0,4,7");

        // Assert
        Assert.That(result, Does.Contain("Error"));
        Assert.That(result, Does.Contain("Failed to generate"));

        TestContext.WriteLine(result);
    }

    [Test]
    public async Task GenerateIntelligentDungeonAsync_WithInvalidPitchClasses_ReturnsErrorMessage()
    {
        // Arrange
        var invalidPitchClasses = "invalid,data,here";

        // Act
        var result = await _plugin!.GenerateIntelligentDungeonAsync(invalidPitchClasses);

        // Assert
        Assert.That(result, Does.Contain("Error"));
        Assert.That(result, Does.Contain("No valid pitch classes"));

        TestContext.WriteLine(result);
    }
}
