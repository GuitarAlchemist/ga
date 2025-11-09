namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Integration tests for analyzer-powered endpoints in GrothendieckController and ChordProgressionsController
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Analyzers")]
[Ignore("GrothendieckController and ChordProgressionsController not yet implemented")]
public class AnalyzerIntegrationTests
{
    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [Test]
    public async Task AnalyzeShapeGraph_WithValidPitchClassSets_ReturnsComprehensiveAnalysis()
    {
        // Arrange
        var request = new
        {
            PitchClassSets = new[] { "0,4,7", "0,3,7", "0,2,7" }, // C major, C minor, Csus2
            MaxFret = 12,
            MaxSpan = 5
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/grothendieck/analyze-shape-graph", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(result.GetProperty("spectralMetrics").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
        Assert.That(result.GetProperty("chordFamilies").GetArrayLength(), Is.GreaterThan(0));
        Assert.That(result.GetProperty("centralShapes").GetArrayLength(), Is.GreaterThan(0));

        TestContext.WriteLine($"Spectral Metrics: {result.GetProperty("spectralMetrics")}");
        TestContext.WriteLine($"Chord Families: {result.GetProperty("chordFamilies").GetArrayLength()}");
        TestContext.WriteLine($"Central Shapes: {result.GetProperty("centralShapes").GetArrayLength()}");
    }

    [Test]
    public async Task AnalyzeShapeGraph_WithEmptyPitchClassSets_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            PitchClassSets = Array.Empty<string>(),
            MaxFret = 12,
            MaxSpan = 5
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/grothendieck/analyze-shape-graph", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AnalyzeShapeGraph_WithLargePitchClassSetCollection_ReturnsAnalysis()
    {
        // Arrange - Test with all diatonic triads in C major
        var request = new
        {
            PitchClassSets = new[]
            {
                "0,4,7", // C major
                "2,5,9", // D minor
                "4,7,11", // E minor
                "5,9,0", // F major
                "7,11,2", // G major
                "9,0,4", // A minor
                "11,2,5" // B diminished
            },
            MaxFret = 12,
            MaxSpan = 5
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/grothendieck/analyze-shape-graph", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var spectralMetrics = result.GetProperty("spectralMetrics");

        Assert.That(spectralMetrics.GetProperty("algebraicConnectivity").GetDouble(), Is.GreaterThan(0));
        Assert.That(spectralMetrics.GetProperty("componentCount").GetInt32(), Is.GreaterThan(0));

        TestContext.WriteLine(
            $"Algebraic Connectivity: {spectralMetrics.GetProperty("algebraicConnectivity").GetDouble()}");
        TestContext.WriteLine($"Component Count: {spectralMetrics.GetProperty("componentCount").GetInt32()}");
    }

    [Test]
    public async Task GeneratePracticePath_WithValidRequest_ReturnsOptimalPath()
    {
        // Arrange
        var request = new
        {
            StartPitchClassSet = "0,4,7", // C major
            TargetPitchClassSet = "0,3,7", // C minor
            Strategy = "Smooth",
            MaxSteps = 5
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/grothendieck/generate-practice-path", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(result.GetProperty("path").GetArrayLength(), Is.GreaterThan(0));
        Assert.That(result.GetProperty("qualityMetrics").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));

        var path = result.GetProperty("path");
        TestContext.WriteLine($"Practice Path Length: {path.GetArrayLength()}");
        TestContext.WriteLine(
            $"Path: {string.Join(" -> ", path.EnumerateArray().Select(p => p.GetProperty("pitchClassSet").GetString()))}");
    }

    [Test]
    public async Task GeneratePracticePath_WithDifferentStrategies_ReturnsVariedPaths()
    {
        // Arrange
        var strategies = new[] { "Smooth", "Diverse", "Challenging" };
        var request = new
        {
            StartPitchClassSet = "0,4,7",
            TargetPitchClassSet = "0,3,7",
            Strategy = "",
            MaxSteps = 5
        };

        foreach (var strategy in strategies)
        {
            // Act
            var modifiedRequest = new
            {
                request.StartPitchClassSet,
                request.TargetPitchClassSet,
                Strategy = strategy,
                request.MaxSteps
            };

            var response = await _client!.PostAsJsonAsync("/api/grothendieck/generate-practice-path", modifiedRequest);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Strategy {strategy} should succeed");

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var metrics = result.GetProperty("qualityMetrics");

            TestContext.WriteLine($"Strategy: {strategy}");
            TestContext.WriteLine($"  Entropy: {metrics.GetProperty("entropy").GetDouble()}");
            TestContext.WriteLine($"  Complexity: {metrics.GetProperty("complexity").GetDouble()}");
            TestContext.WriteLine($"  Predictability: {metrics.GetProperty("predictability").GetDouble()}");
        }
    }

    [Test]
    public async Task AnalyzeProgression_WithValidChordSequence_ReturnsAnalysis()
    {
        // Arrange
        var request = new
        {
            PitchClassSets = new[] { "0,4,7", "5,9,0", "7,11,2", "0,4,7" } // I-IV-V-I in C major
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/chord-progressions/analyze", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(result.GetProperty("entropy").GetDouble(), Is.GreaterThan(0));
        Assert.That(result.GetProperty("complexity").GetDouble(), Is.GreaterThanOrEqualTo(0));
        Assert.That(result.GetProperty("stability").GetDouble(), Is.GreaterThanOrEqualTo(0));

        TestContext.WriteLine($"Entropy: {result.GetProperty("entropy").GetDouble()}");
        TestContext.WriteLine($"Complexity: {result.GetProperty("complexity").GetDouble()}");
        TestContext.WriteLine($"Stability: {result.GetProperty("stability").GetDouble()}");
        TestContext.WriteLine($"Predictability: {result.GetProperty("predictability").GetDouble()}");
    }

    [Test]
    public async Task AnalyzeProgression_WithRepetitiveSequence_ShowsLowEntropy()
    {
        // Arrange - Same chord repeated
        var request = new
        {
            PitchClassSets = new[] { "0,4,7", "0,4,7", "0,4,7", "0,4,7" }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/chord-progressions/analyze", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entropy = result.GetProperty("entropy").GetDouble();

        Assert.That(entropy, Is.LessThan(1.0), "Repetitive sequence should have low entropy");
        TestContext.WriteLine($"Entropy of repetitive sequence: {entropy}");
    }

    [Test]
    public async Task AnalyzeProgression_WithComplexJazzSequence_ShowsHighComplexity()
    {
        // Arrange - Complex jazz progression with extensions
        var request = new
        {
            PitchClassSets = new[]
            {
                "0,4,7,11", // Cmaj7
                "2,5,9,0", // Dm7
                "7,11,2,5", // G7
                "0,4,7,11" // Cmaj7
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/chord-progressions/analyze", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var complexity = result.GetProperty("complexity").GetDouble();

        Assert.That(complexity, Is.GreaterThan(0), "Jazz progression should show measurable complexity");
        TestContext.WriteLine($"Complexity of jazz progression: {complexity}");
    }

    [Test]
    public async Task AnalyzeProgression_WithEmptySequence_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            PitchClassSets = Array.Empty<string>()
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/chord-progressions/analyze", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetChordsForKey_ReturnsEnrichedChords_WithCentralityData()
    {
        // Arrange
        var key = "C";

        // Act
        var response = await _client!.GetAsync($"/api/contextual-chords/key/{key}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var chords = result.GetProperty("chords").EnumerateArray().ToList();

        Assert.That(chords.Count, Is.GreaterThan(0));

        // Check that at least some chords have centrality data
        var chordsWithCentrality = chords.Where(c =>
            c.TryGetProperty("centrality", out var cent) && cent.GetDouble() > 0).ToList();

        Assert.That(chordsWithCentrality.Count, Is.GreaterThan(0),
            "Some chords should have centrality scores");

        TestContext.WriteLine($"Total chords: {chords.Count}");
        TestContext.WriteLine($"Chords with centrality > 0: {chordsWithCentrality.Count}");

        // Log central chords
        foreach (var chord in chordsWithCentrality.Take(5))
        {
            TestContext.WriteLine($"  {chord.GetProperty("contextualName").GetString()}: " +
                                  $"Centrality={chord.GetProperty("centrality").GetDouble():F3}, " +
                                  $"IsCentral={chord.GetProperty("isCentral").GetBoolean()}");
        }
    }
}
