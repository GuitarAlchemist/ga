namespace GaApi.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Integration tests for Guitar Agent endpoints powered by Microsoft Agents Framework
///     Tests the /api/agents/guitar/* endpoints for progression generation, spicing up, and reharmonization
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("GuitarAgents")]
public class GuitarAgentsIntegrationTests
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
    public async Task SpiceUpProgression_WithValidRequest_ReturnsStructuredResponse()
    {
        // Arrange
        var request = new
        {
            Progression = new[] { "Dm7", "G7", "Cmaj7", "A7" },
            Key = "C Major",
            Style = "bossa nova",
            Mood = "lush",
            PreserveCadence = true,
            FavorCloseVoicings = true,
            Notes = "Player favours drop-2 shapes around 5th position."
        };

        // Act
        TestContext.WriteLine("=== Spice Up Progression Test ===");
        TestContext.WriteLine(
            $"Request: {JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true })}");

        var response = await _client!.PostAsJsonAsync("/api/agents/guitar/progressions/spice-up", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        TestContext.WriteLine($"Response Content: {content}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Expected OK but got {response.StatusCode}. Content: {content}");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Validate response structure
        Assert.That(result.TryGetProperty("title", out var title), Is.True, "Response should have 'title' property");
        Assert.That(result.TryGetProperty("summary", out var summary), Is.True,
            "Response should have 'summary' property");
        Assert.That(result.TryGetProperty("progression", out var progression), Is.True,
            "Response should have 'progression' property");
        Assert.That(result.TryGetProperty("sections", out var sections), Is.True,
            "Response should have 'sections' property");
        Assert.That(result.TryGetProperty("practiceIdeas", out var practiceIdeas), Is.True,
            "Response should have 'practiceIdeas' property");
        Assert.That(result.TryGetProperty("warnings", out var warnings), Is.True,
            "Response should have 'warnings' property");

        // Validate progression is not empty
        Assert.That(progression.GetArrayLength(), Is.GreaterThan(0), "Progression should contain chords");

        // Log response details
        TestContext.WriteLine($"\nTitle: {title.GetString()}");
        TestContext.WriteLine($"Summary: {summary.GetString()}");
        TestContext.WriteLine(
            $"Progression: {string.Join(", ", progression.EnumerateArray().Select(c => c.GetString()))}");
        TestContext.WriteLine($"Sections Count: {sections.GetArrayLength()}");
        TestContext.WriteLine($"Practice Ideas Count: {practiceIdeas.GetArrayLength()}");

        if (result.TryGetProperty("tokenUsage", out var tokenUsage) && tokenUsage.ValueKind != JsonValueKind.Null)
        {
            TestContext.WriteLine("\nToken Usage:");
            if (tokenUsage.TryGetProperty("inputTokens", out var inputTokens))
            {
                TestContext.WriteLine($"  Input: {inputTokens.GetInt64()}");
            }

            if (tokenUsage.TryGetProperty("outputTokens", out var outputTokens))
            {
                TestContext.WriteLine($"  Output: {outputTokens.GetInt64()}");
            }

            if (tokenUsage.TryGetProperty("totalTokens", out var totalTokens))
            {
                TestContext.WriteLine($"  Total: {totalTokens.GetInt64()}");
            }
        }
    }

    [Test]
    public async Task SpiceUpProgression_WithMinimalRequest_ReturnsValidResponse()
    {
        // Arrange - Minimal required fields only
        var request = new
        {
            Progression = new[] { "C", "Am", "F", "G" }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agents/guitar/progressions/spice-up", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(result.GetProperty("progression").GetArrayLength(), Is.GreaterThan(0));
            TestContext.WriteLine(
                $"Progression: {string.Join(", ", result.GetProperty("progression").EnumerateArray().Select(c => c.GetString()))}");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Error: {errorContent}");
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task SpiceUpProgression_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange - Invalid: progression with only 1 chord (requires minimum 2)
        var request = new
        {
            Progression = new[] { "C" }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agents/guitar/progressions/spice-up", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ReharmonizeProgression_WithValidRequest_ReturnsStructuredResponse()
    {
        // Arrange
        var request = new
        {
            Progression = new[] { "Am7", "D7", "Gmaj7", "Cmaj7" },
            Key = "G Major",
            Style = "modern jazz",
            TargetFeel = "darker bridge with tension release",
            LockFirstChord = true,
            LockLastChord = true,
            AllowModalInterchange = true,
            Notes = "Ideally keep things playable in 7th to 10th fret region."
        };

        // Act
        TestContext.WriteLine("=== Reharmonize Progression Test ===");
        TestContext.WriteLine(
            $"Request: {JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true })}");

        var response = await _client!.PostAsJsonAsync("/api/agents/guitar/progressions/reharmonize", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        TestContext.WriteLine($"Response Content: {content}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Expected OK but got {response.StatusCode}. Content: {content}");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Validate response structure
        Assert.That(result.TryGetProperty("title", out var title), Is.True);
        Assert.That(result.TryGetProperty("summary", out var summary), Is.True);
        Assert.That(result.TryGetProperty("progression", out var progression), Is.True);
        Assert.That(result.TryGetProperty("sections", out var sections), Is.True);

        // Validate progression
        Assert.That(progression.GetArrayLength(), Is.GreaterThan(0));

        // Log response details
        TestContext.WriteLine($"\nTitle: {title.GetString()}");
        TestContext.WriteLine($"Summary: {summary.GetString()}");
        TestContext.WriteLine(
            $"Progression: {string.Join(", ", progression.EnumerateArray().Select(c => c.GetString()))}");
        TestContext.WriteLine($"Sections Count: {sections.GetArrayLength()}");

        // Log sections
        foreach (var section in sections.EnumerateArray())
        {
            TestContext.WriteLine($"\nSection: {section.GetProperty("focus").GetString()}");
            TestContext.WriteLine(
                $"  Chords: {string.Join(", ", section.GetProperty("chords").EnumerateArray().Select(c => c.GetString()))}");
            TestContext.WriteLine($"  Description: {section.GetProperty("description").GetString()}");
        }
    }

    [Test]
    public async Task ReharmonizeProgression_WithMinimalRequest_ReturnsValidResponse()
    {
        // Arrange - Minimal required fields
        var request = new
        {
            Progression = new[] { "C", "F", "G", "C" }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agents/guitar/progressions/reharmonize", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(result.GetProperty("progression").GetArrayLength(), Is.GreaterThan(0));
            TestContext.WriteLine(
                $"Reharmonized: {string.Join(", ", result.GetProperty("progression").EnumerateArray().Select(c => c.GetString()))}");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Error: {errorContent}");
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CreateProgression_WithValidRequest_ReturnsStructuredResponse()
    {
        // Arrange
        var request = new
        {
            Key = "E Minor",
            Mode = "Dorian",
            Genre = "lofi chill",
            Mood = "dreamy",
            SkillLevel = "intermediate",
            Bars = 8,
            ReferenceArtists = new[] { "Khruangbin", "Tom Misch" },
            Notes = "Would love some chromatic bass walks and dreamy tensions."
        };

        // Act
        TestContext.WriteLine("=== Create Progression Test ===");
        TestContext.WriteLine(
            $"Request: {JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true })}");

        var response = await _client!.PostAsJsonAsync("/api/agents/guitar/progressions/create", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        TestContext.WriteLine($"Response Content: {content}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Expected OK but got {response.StatusCode}. Content: {content}");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Validate response structure
        Assert.That(result.TryGetProperty("title", out var title), Is.True);
        Assert.That(result.TryGetProperty("summary", out var summary), Is.True);
        Assert.That(result.TryGetProperty("progression", out var progression), Is.True);
        Assert.That(result.TryGetProperty("sections", out var sections), Is.True);
        Assert.That(result.TryGetProperty("practiceIdeas", out var practiceIdeas), Is.True);

        // Validate progression
        Assert.That(progression.GetArrayLength(), Is.GreaterThan(0));

        // Log response details
        TestContext.WriteLine($"\nTitle: {title.GetString()}");
        TestContext.WriteLine($"Summary: {summary.GetString()}");
        TestContext.WriteLine(
            $"Progression: {string.Join(", ", progression.EnumerateArray().Select(c => c.GetString()))}");
        TestContext.WriteLine($"Sections Count: {sections.GetArrayLength()}");
        TestContext.WriteLine($"Practice Ideas Count: {practiceIdeas.GetArrayLength()}");

        // Log practice ideas
        TestContext.WriteLine("\nPractice Ideas:");
        foreach (var idea in practiceIdeas.EnumerateArray())
        {
            TestContext.WriteLine($"  - {idea.GetString()}");
        }
    }

    [Test]
    public async Task CreateProgression_WithMinimalRequest_ReturnsValidResponse()
    {
        // Arrange - Only required field
        var request = new
        {
            Key = "C Major"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agents/guitar/progressions/create", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(result.GetProperty("progression").GetArrayLength(), Is.GreaterThan(0));
            TestContext.WriteLine(
                $"Created Progression: {string.Join(", ", result.GetProperty("progression").EnumerateArray().Select(c => c.GetString()))}");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Error: {errorContent}");
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CreateProgression_WithInvalidBars_ReturnsBadRequest()
    {
        // Arrange - Invalid: bars out of range (must be 4-32)
        var request = new
        {
            Key = "C Major",
            Bars = 100
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agents/guitar/progressions/create", request);

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
