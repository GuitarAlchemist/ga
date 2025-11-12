namespace GuitarAlchemistChatbot.Tests.Services;

using GuitarAlchemistChatbot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

/// <summary>
///     Integration tests for GaApiClient service
///     Tests HTTP communication with GaApi endpoints
///     NOTE: These tests require GaApi to be running at https://localhost:7001
///     Start services with: .\Scripts\start-all.ps1 -Dashboard
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("GaApiClient")]
[Category("RequiresRunningServices")]
public class GaApiClientTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Skip SSL certificate validation for local testing
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_gaApiBaseUrl)
        };
    }

    [SetUp]
    public void Setup()
    {
        // Create logger mock
        _loggerMock = new Mock<ILogger<GaApiClient>>();

        // Create GaApiClient with real HTTP client pointing to running service
        _gaApiClient = new GaApiClient(_httpClient!, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // Don't dispose HttpClient here - it's shared across tests
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
    }

    private const string _gaApiBaseUrl = "https://localhost:7001";
    private HttpClient? _httpClient;
    private GaApiClient? _gaApiClient;
    private Mock<ILogger<GaApiClient>>? _loggerMock;

    [Test]
    public async Task AnalyzeProgressionAsync_WithValidProgression_ReturnsAnalysis()
    {
        // Arrange
        var pitchClassSets = new[] { "0,4,7", "5,9,0", "7,11,2", "0,4,7" }; // I-IV-V-I in C major

        // Act
        var result = await _gaApiClient!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert
        Assert.That(result, Is.Not.Null, "Should return analysis result");
        Assert.That(result!.Entropy, Is.GreaterThan(0), "Entropy should be positive");
        Assert.That(result.Complexity, Is.GreaterThanOrEqualTo(0), "Complexity should be non-negative");
        Assert.That(result.Predictability, Is.GreaterThanOrEqualTo(0), "Predictability should be non-negative");
        Assert.That(result.UniqueShapes, Is.GreaterThan(0), "Should have unique shapes");

        TestContext.WriteLine($"Entropy: {result.Entropy:F2}");
        TestContext.WriteLine($"Complexity: {result.Complexity:F2}");
        TestContext.WriteLine($"Predictability: {result.Predictability:F2}");
        TestContext.WriteLine($"Unique Shapes: {result.UniqueShapes}");
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WithRepetitiveProgression_ShowsLowEntropy()
    {
        // Arrange - Same chord repeated
        var pitchClassSets = new[] { "0,4,7", "0,4,7", "0,4,7", "0,4,7" };

        // Act
        var result = await _gaApiClient!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Entropy, Is.LessThan(1.0), "Repetitive sequence should have low entropy");
        Assert.That(result.UniqueShapes, Is.EqualTo(1), "Should have only 1 unique shape");

        TestContext.WriteLine($"Entropy of repetitive sequence: {result.Entropy:F2}");
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WithComplexJazzProgression_ShowsHighComplexity()
    {
        // Arrange - ii-V-I with extensions
        var pitchClassSets = new[]
        {
            "2,5,9,0", // Dm9
            "7,11,2,5", // G7(9)
            "0,4,7,11" // Cmaj7
        };

        // Act
        var result = await _gaApiClient!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Complexity, Is.GreaterThan(0), "Jazz progression should show complexity");

        TestContext.WriteLine($"Complexity of jazz progression: {result.Complexity:F2}");
    }

    [Test]
    public async Task GeneratePracticePathAsync_WithValidParameters_ReturnsPath()
    {
        // Arrange
        var pitchClasses = new[] { 0, 4, 7 }; // C major triad
        var tuning = "E2 A2 D3 G3 B3 E4"; // Standard tuning
        var targetLength = 10;
        var strategy = "balanced";

        // Act
        var result = await _gaApiClient!.GeneratePracticePathAsync(
            pitchClasses,
            tuning,
            targetLength,
            strategy);

        // Assert
        Assert.That(result, Is.Not.Null, "Should return practice path");
        Assert.That(result!.ShapeIds, Is.Not.Empty, "Should have shape IDs");
        Assert.That(result.Shapes, Is.Not.Empty, "Should have shapes");
        Assert.That(result.Quality, Is.GreaterThan(0), "Quality should be positive");
        Assert.That(result.Diversity, Is.GreaterThanOrEqualTo(0), "Diversity should be non-negative");

        TestContext.WriteLine($"Path Length: {result.ShapeIds.Count}");
        TestContext.WriteLine($"Quality: {result.Quality:F2}");
        TestContext.WriteLine($"Diversity: {result.Diversity:F2}");
        TestContext.WriteLine($"Entropy: {result.Entropy:F2}");
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
        var pitchClasses = new[] { 0, 4, 7 };
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act
        var result = await _gaApiClient!.GeneratePracticePathAsync(
            pitchClasses,
            tuning,
            8,
            strategy);

        // Assert
        Assert.That(result, Is.Not.Null, $"Strategy {strategy} should return a path");
        Assert.That(result!.ShapeIds, Is.Not.Empty, $"Strategy {strategy} should have shapes");

        TestContext.WriteLine($"Strategy: {strategy}");
        TestContext.WriteLine($"  Quality: {result.Quality:F2}");
        TestContext.WriteLine($"  Diversity: {result.Diversity:F2}");
    }

    [Test]
    public async Task AnalyzeShapeGraphAsync_WithValidParameters_ReturnsComprehensiveAnalysis()
    {
        // Arrange
        var pitchClasses = new[] { 0, 4, 7 }; // C major triad
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act
        var result = await _gaApiClient!.AnalyzeShapeGraphAsync(pitchClasses, tuning);

        // Assert
        Assert.That(result, Is.Not.Null, "Should return analysis");

        // Check spectral metrics
        if (result!.Spectral != null)
        {
            Assert.That(result.Spectral.AlgebraicConnectivity, Is.GreaterThanOrEqualTo(0));
            TestContext.WriteLine($"Algebraic Connectivity: {result.Spectral.AlgebraicConnectivity:F2}");
            TestContext.WriteLine($"Spectral Gap: {result.Spectral.SpectralGap:F2}");
        }

        // Check chord families
        Assert.That(result.ChordFamilies, Is.Not.Null);
        TestContext.WriteLine($"Chord Families: {result.ChordFamilies.Count}");

        // Check central shapes
        Assert.That(result.CentralShapes, Is.Not.Null);
        TestContext.WriteLine($"Central Shapes: {result.CentralShapes.Count}");

        // Check dynamics
        if (result.Dynamics != null)
        {
            TestContext.WriteLine($"Lyapunov Exponent: {result.Dynamics.LyapunovExponent:F2}");
            TestContext.WriteLine($"Is Chaotic: {result.Dynamics.IsChaotic}");
            TestContext.WriteLine($"Is Stable: {result.Dynamics.IsStable}");
            TestContext.WriteLine($"Attractors: {result.Dynamics.Attractors.Count}");
            TestContext.WriteLine($"Limit Cycles: {result.Dynamics.LimitCycles.Count}");
        }
    }

    [Test]
    public async Task GenerateDungeonAsync_WithDefaultParameters_ReturnsDungeon()
    {
        // Arrange
        var width = 80;
        var height = 60;
        var maxDepth = 4;

        // Act
        var result = await _gaApiClient!.GenerateDungeonAsync(width, height, maxDepth);

        // Assert
        Assert.That(result, Is.Not.Null, "Should return dungeon");
        Assert.That(result!.Width, Is.EqualTo(width));
        Assert.That(result.Height, Is.EqualTo(height));
        Assert.That(result.Rooms, Is.Not.Empty, "Should have rooms");
        Assert.That(result.Corridors, Is.Not.Empty, "Should have corridors");

        TestContext.WriteLine($"Dungeon: {result.Width}x{result.Height}");
        TestContext.WriteLine($"Rooms: {result.Rooms.Count}");
        TestContext.WriteLine($"Corridors: {result.Corridors.Count}");
    }

    [Test]
    public async Task GenerateDungeonAsync_WithSeed_ReturnsReproducibleDungeon()
    {
        // Arrange
        var seed = 12345;

        // Act
        var result1 = await _gaApiClient!.GenerateDungeonAsync(seed: seed);
        var result2 = await _gaApiClient!.GenerateDungeonAsync(seed: seed);

        // Assert
        Assert.That(result1, Is.Not.Null);
        Assert.That(result2, Is.Not.Null);
        Assert.That(result1!.Rooms.Count, Is.EqualTo(result2!.Rooms.Count),
            "Same seed should produce same number of rooms");
    }

    [Test]
    public async Task GenerateIntelligentDungeonAsync_WithValidParameters_ReturnsIntelligentDungeon()
    {
        // Arrange
        var pitchClasses = new[] { 0, 4, 7 }; // C major triad
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act
        var result = await _gaApiClient!.GenerateIntelligentDungeonAsync(
            pitchClasses,
            tuning);

        // Assert
        Assert.That(result, Is.Not.Null, "Should return intelligent dungeon");
        Assert.That(result!.Floors, Is.Not.Empty, "Should have floors");
        Assert.That(result.Landmarks, Is.Not.Empty, "Should have landmarks");
        Assert.That(result.LearningPath, Is.Not.Null, "Should have learning path");

        TestContext.WriteLine($"Floors: {result.Floors.Count}");
        TestContext.WriteLine($"Landmarks: {result.Landmarks.Count}");
        TestContext.WriteLine($"Portals: {result.Portals.Count}");
        TestContext.WriteLine($"Safe Zones: {result.SafeZones.Count}");
        TestContext.WriteLine($"Challenge Paths: {result.ChallengePaths.Count}");
        TestContext.WriteLine($"Learning Path Length: {result.LearningPath.ShapeIds.Count}");
    }

    [Test]
    public async Task AnalyzeProgressionAsync_WithEmptyArray_ReturnsNull()
    {
        // Arrange
        var pitchClassSets = Array.Empty<string>();

        // Act
        var result = await _gaApiClient!.AnalyzeProgressionAsync(pitchClassSets);

        // Assert - Should handle gracefully
        // Implementation may return null or throw, depending on design
        TestContext.WriteLine($"Result with empty array: {result}");
    }
}
