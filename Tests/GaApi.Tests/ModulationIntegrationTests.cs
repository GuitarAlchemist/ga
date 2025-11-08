namespace GaApi.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Models;

[TestFixture]
public class ModulationIntegrationTests
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
        _client.Dispose();
        _factory.Dispose();
    }

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Test]
    public async Task GetModulationSuggestion_CMajorToGMajor_ReturnsSuccess()
    {
        // Arrange
        var sourceKey = "C Major";
        var targetKey = "G Major";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation?sourceKey={Uri.EscapeDataString(sourceKey)}&targetKey={Uri.EscapeDataString(targetKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ModulationSuggestionDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SourceKey, Does.Contain("C"));
        Assert.That(result.TargetKey, Does.Contain("G"));
        Assert.That(result.ModulationType, Is.EqualTo("Dominant"));
        Assert.That(result.PivotChords, Is.Not.Empty);
        Assert.That(result.Description, Is.Not.Empty);
        Assert.That(result.Difficulty, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0));
    }

    [Test]
    public async Task GetModulationSuggestion_CMajorToAMinor_ReturnsRelativeModulation()
    {
        // Arrange
        var sourceKey = "C Major";
        var targetKey = "A Minor";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation?sourceKey={Uri.EscapeDataString(sourceKey)}&targetKey={Uri.EscapeDataString(targetKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ModulationSuggestionDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ModulationType, Is.EqualTo("Relative"));
        Assert.That(result.Difficulty, Is.LessThan(0.2)); // Relative modulation should be easy
        Assert.That(result.PivotChords.Count, Is.GreaterThan(0)); // Should have many pivot chords
    }

    [Test]
    public async Task GetModulationSuggestion_CMajorToCMinor_ReturnsParallelModulation()
    {
        // Arrange
        var sourceKey = "C Major";
        var targetKey = "C Minor";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation?sourceKey={Uri.EscapeDataString(sourceKey)}&targetKey={Uri.EscapeDataString(targetKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ModulationSuggestionDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ModulationType, Is.EqualTo("Parallel"));
        Assert.That(result.Description, Does.Contain("Parallel"));
    }

    [Test]
    public async Task GetModulationSuggestion_CMajorToFMajor_ReturnsSubdominantModulation()
    {
        // Arrange
        var sourceKey = "C Major";
        var targetKey = "F Major";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation?sourceKey={Uri.EscapeDataString(sourceKey)}&targetKey={Uri.EscapeDataString(targetKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ModulationSuggestionDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ModulationType, Is.EqualTo("Subdominant"));
    }

    [Test]
    public async Task GetModulationSuggestion_InvalidSourceKey_ReturnsBadRequest()
    {
        // Arrange
        var sourceKey = "Invalid Key";
        var targetKey = "G Major";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation?sourceKey={Uri.EscapeDataString(sourceKey)}&targetKey={Uri.EscapeDataString(targetKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetModulationSuggestion_InvalidTargetKey_ReturnsBadRequest()
    {
        // Arrange
        var sourceKey = "C Major";
        var targetKey = "Invalid Key";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation?sourceKey={Uri.EscapeDataString(sourceKey)}&targetKey={Uri.EscapeDataString(targetKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetCommonModulations_CMajor_ReturnsMultipleModulations()
    {
        // Arrange
        var sourceKey = "C Major";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation/common?sourceKey={Uri.EscapeDataString(sourceKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<List<ModulationSuggestionDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.GreaterThan(0));

        // Should include relative, parallel, dominant, subdominant
        Assert.That(result.Count, Is.GreaterThanOrEqualTo(4));

        // Should be ordered by difficulty (easiest first)
        for (var i = 0; i < result.Count - 1; i++)
        {
            Assert.That(result[i].Difficulty, Is.LessThanOrEqualTo(result[i + 1].Difficulty));
        }
    }

    [Test]
    public async Task GetCommonModulations_InvalidKey_ReturnsBadRequest()
    {
        // Arrange
        var sourceKey = "Invalid Key";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation/common?sourceKey={Uri.EscapeDataString(sourceKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetModulationSuggestion_HasPivotChords_ReturnsPivotChordDetails()
    {
        // Arrange
        var sourceKey = "C Major";
        var targetKey = "G Major";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation?sourceKey={Uri.EscapeDataString(sourceKey)}&targetKey={Uri.EscapeDataString(targetKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ModulationSuggestionDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PivotChords, Is.Not.Empty);

        // Check pivot chord structure
        var firstPivot = result.PivotChords.First();
        Assert.That(firstPivot.ChordName, Is.Not.Empty);
        Assert.That(firstPivot.RomanNumeralInSourceKey, Is.Not.Empty);
        Assert.That(firstPivot.RomanNumeralInTargetKey, Is.Not.Empty);
        Assert.That(firstPivot.Function, Is.Not.Empty);
    }

    [Test]
    public async Task GetModulationSuggestion_HasSuggestedProgression_ReturnsProgression()
    {
        // Arrange
        var sourceKey = "C Major";
        var targetKey = "G Major";

        // Act
        var response =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation?sourceKey={Uri.EscapeDataString(sourceKey)}&targetKey={Uri.EscapeDataString(targetKey)}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ModulationSuggestionDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SuggestedProgression, Is.Not.Empty);
        Assert.That(result.SuggestedProgression.Count, Is.GreaterThanOrEqualTo(2)); // At least start and end
    }

    [Test]
    public async Task GetCommonModulations_MultipleCalls_UsesCache()
    {
        // Arrange
        var sourceKey = "C Major";

        // Act - First call
        var response1 =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation/common?sourceKey={Uri.EscapeDataString(sourceKey)}");
        var result1 = await response1.Content.ReadFromJsonAsync<List<ModulationSuggestionDto>>();

        // Act - Second call (should use cache)
        var response2 =
            await _client.GetAsync(
                $"/api/contextual-chords/modulation/common?sourceKey={Uri.EscapeDataString(sourceKey)}");
        var result2 = await response2.Content.ReadFromJsonAsync<List<ModulationSuggestionDto>>();

        // Assert - Results should be identical
        Assert.That(result1, Is.Not.Null);
        Assert.That(result2, Is.Not.Null);
        Assert.That(result1!.Count, Is.EqualTo(result2!.Count));

        for (var i = 0; i < result1.Count; i++)
        {
            Assert.That(result1[i].TargetKey, Is.EqualTo(result2[i].TargetKey));
            Assert.That(result1[i].ModulationType, Is.EqualTo(result2[i].ModulationType));
        }
    }
}
