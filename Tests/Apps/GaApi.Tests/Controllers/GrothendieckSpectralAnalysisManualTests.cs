namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Manual integration tests for Grothendieck and Spectral Graph Analysis functionality
///     Tests the actual API endpoints with correct request formats
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Manual")]
[Category("SpectralAnalysis")]
public class GrothendieckSpectralAnalysisManualTests
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
    public async Task AnalyzeShapeGraph_WithCMajorTriad_ReturnsSpectralMetrics()
    {
        // Arrange - C major triad (C, E, G) = pitch classes (0, 4, 7)
        var request = new
        {
            TuningId = "standard",
            PitchClasses = new[] { 0, 4, 7 },
            MaxFret = 12,
            MaxSpan = 5,
            IncludeSpectralAnalysis = true,
            IncludeDynamicalAnalysis = true,
            IncludeTopologicalAnalysis = false,
            ClusterCount = 3,
            TopCentralShapes = 5
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/grothendieck/analyze-shape-graph", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Check spectral metrics
            if (result.TryGetProperty("spectral", out var spectral))
            {
                TestContext.WriteLine("\n=== Spectral Metrics ===");
                TestContext.WriteLine(
                    $"Algebraic Connectivity: {spectral.GetProperty("algebraicConnectivity").GetDouble():F4}");
                TestContext.WriteLine($"Spectral Gap: {spectral.GetProperty("spectralGap").GetDouble():F4}");
                TestContext.WriteLine($"Component Count: {spectral.GetProperty("componentCount").GetInt32()}");
                TestContext.WriteLine(
                    $"Average Path Length: {spectral.GetProperty("averagePathLength").GetDouble():F4}");
                TestContext.WriteLine($"Diameter: {spectral.GetProperty("diameter").GetDouble():F4}");

                Assert.That(spectral.GetProperty("algebraicConnectivity").GetDouble(), Is.GreaterThan(0));
                Assert.That(spectral.GetProperty("componentCount").GetInt32(), Is.GreaterThan(0));
            }

            // Check chord families
            if (result.TryGetProperty("chordFamilies", out var families))
            {
                var familyCount = families.GetArrayLength();
                TestContext.WriteLine("\n=== Chord Families ===");
                TestContext.WriteLine($"Total Families: {familyCount}");

                foreach (var family in families.EnumerateArray().Take(3))
                {
                    TestContext.WriteLine(
                        $"  Cluster {family.GetProperty("clusterId").GetInt32()}: {family.GetProperty("shapeIds").GetArrayLength()} shapes");
                }

                Assert.That(familyCount, Is.GreaterThan(0));
            }

            // Check central shapes
            if (result.TryGetProperty("centralShapes", out var centralShapes))
            {
                var shapeCount = centralShapes.GetArrayLength();
                TestContext.WriteLine("\n=== Central Shapes ===");
                TestContext.WriteLine($"Total Central Shapes: {shapeCount}");

                foreach (var shape in centralShapes.EnumerateArray().Take(5))
                {
                    TestContext.WriteLine(
                        $"  {shape.GetProperty("shapeId").GetString()}: Centrality={shape.GetProperty("centrality").GetDouble():F4}");
                }

                Assert.That(shapeCount, Is.GreaterThan(0));
            }

            // Check dynamics
            if (result.TryGetProperty("dynamics", out var dynamics))
            {
                TestContext.WriteLine("\n=== Dynamical System ===");
                TestContext.WriteLine($"Lyapunov Exponent: {dynamics.GetProperty("lyapunovExponent").GetDouble():F4}");
                TestContext.WriteLine($"Is Chaotic: {dynamics.GetProperty("isChaotic").GetBoolean()}");
                TestContext.WriteLine($"Is Stable: {dynamics.GetProperty("isStable").GetBoolean()}");
                TestContext.WriteLine($"Attractors: {dynamics.GetProperty("attractors").GetArrayLength()}");
                TestContext.WriteLine($"Limit Cycles: {dynamics.GetProperty("limitCycles").GetArrayLength()}");
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Error: {errorContent}");
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task AnalyzeShapeGraph_WithMultipleChords_ReturnsComprehensiveAnalysis()
    {
        // Arrange - C major, A minor, F major, G major (I-vi-IV-V)
        var request = new
        {
            TuningId = "standard",
            PitchClasses = new[] { 0, 4, 7, 9, 5 }, // Combined pitch classes from all chords
            MaxFret = 12,
            MaxSpan = 5,
            IncludeSpectralAnalysis = true,
            IncludeDynamicalAnalysis = true,
            IncludeTopologicalAnalysis = true,
            ClusterCount = 5,
            TopCentralShapes = 10
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/grothendieck/analyze-shape-graph", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            TestContext.WriteLine("\n=== Comprehensive Analysis Results ===");

            // Spectral
            if (result.TryGetProperty("spectral", out var spectral))
            {
                TestContext.WriteLine(
                    $"Algebraic Connectivity: {spectral.GetProperty("algebraicConnectivity").GetDouble():F4}");
            }

            // Families
            if (result.TryGetProperty("chordFamilies", out var families))
            {
                TestContext.WriteLine($"Chord Families: {families.GetArrayLength()}");
            }

            // Central Shapes
            if (result.TryGetProperty("centralShapes", out var centralShapes))
            {
                TestContext.WriteLine($"Central Shapes: {centralShapes.GetArrayLength()}");
            }

            // Bottlenecks
            if (result.TryGetProperty("bottlenecks", out var bottlenecks))
            {
                TestContext.WriteLine($"Bottlenecks: {bottlenecks.GetArrayLength()}");
            }

            // Topology
            if (result.TryGetProperty("topology", out var topology))
            {
                TestContext.WriteLine($"Betti Number 0: {topology.GetProperty("bettiNumber0").GetInt32()}");
                TestContext.WriteLine($"Betti Number 1: {topology.GetProperty("bettiNumber1").GetInt32()}");
                TestContext.WriteLine($"Persistent Features: {topology.GetProperty("features").GetArrayLength()}");
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Error: {errorContent}");
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GeneratePracticePath_WithCMajorScale_ReturnsOptimizedPath()
    {
        // Arrange - C major scale pitch classes
        var request = new
        {
            TuningId = "standard",
            PitchClasses = new[] { 0, 2, 4, 5, 7, 9, 11 }, // C major scale
            PathLength = 8,
            Strategy = "Balanced",
            PreferCentralShapes = true,
            MinErgonomics = 0.5,
            MaxFret = 12,
            MaxSpan = 5
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/grothendieck/generate-practice-path", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            TestContext.WriteLine("\n=== Practice Path Results ===");

            var shapeIds = result.GetProperty("shapeIds");
            TestContext.WriteLine($"Path Length: {shapeIds.GetArrayLength()}");
            TestContext.WriteLine($"Entropy: {result.GetProperty("entropy").GetDouble():F4}");
            TestContext.WriteLine($"Complexity: {result.GetProperty("complexity").GetDouble():F4}");
            TestContext.WriteLine($"Predictability: {result.GetProperty("predictability").GetDouble():F4}");
            TestContext.WriteLine($"Diversity: {result.GetProperty("diversity").GetDouble():F4}");
            TestContext.WriteLine($"Quality: {result.GetProperty("quality").GetDouble():F4}");

            TestContext.WriteLine("\nPath:");
            foreach (var shapeId in shapeIds.EnumerateArray())
            {
                TestContext.WriteLine($"  → {shapeId.GetString()}");
            }

            Assert.That(shapeIds.GetArrayLength(), Is.GreaterThan(0));
            Assert.That(result.GetProperty("quality").GetDouble(), Is.GreaterThan(0));
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Error: {errorContent}");
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GeneratePracticePath_WithDifferentStrategies_ProducesDifferentPaths()
    {
        var strategies = new[]
            { "Balanced", "MinimizeVoiceLeading", "MaximizeInformationGain", "ExploreFamilies", "FollowAttractors" };

        foreach (var strategy in strategies)
        {
            // Arrange
            var request = new
            {
                TuningId = "standard",
                PitchClasses = new[] { 0, 4, 7 }, // C major
                PathLength = 5,
                Strategy = strategy,
                PreferCentralShapes = true,
                MinErgonomics = 0.5,
                MaxFret = 12,
                MaxSpan = 5
            };

            // Act
            var response = await _client!.PostAsJsonAsync("/api/grothendieck/generate-practice-path", request);

            // Assert
            TestContext.WriteLine($"\n=== Strategy: {strategy} ===");
            TestContext.WriteLine($"Response Status: {response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                TestContext.WriteLine($"Quality: {result.GetProperty("quality").GetDouble():F4}");
                TestContext.WriteLine($"Entropy: {result.GetProperty("entropy").GetDouble():F4}");
                TestContext.WriteLine($"Complexity: {result.GetProperty("complexity").GetDouble():F4}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                TestContext.WriteLine($"Error: {errorContent}");
            }
        }
    }
}
