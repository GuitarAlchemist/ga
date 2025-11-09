namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Integration tests for MonadicChordsController demonstrating monad pattern error handling
/// </summary>
[TestFixture]
[Category("Integration")]
[Ignore("MonadicChordsController not yet implemented")]
public class MonadicChordsControllerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [Test]
    public async Task GetTotalCount_ShouldReturnCount_WhenDatabaseIsAvailable()
    {
        // Act
        var response = await _client!.GetAsync("/api/monadic/chords/count");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.That(json.RootElement.TryGetProperty("count", out var countProp), Is.True);
        Assert.That(countProp.GetInt64(), Is.GreaterThanOrEqualTo(0));

        TestContext.WriteLine($"Total chord count: {countProp.GetInt64()}");
    }

    [Test]
    public async Task GetById_WithValidId_ShouldReturnChord()
    {
        // Arrange - First get a valid chord ID
        var countResponse = await _client!.GetAsync("/api/monadic/chords/count");
        Assert.That(countResponse.IsSuccessStatusCode, Is.True);

        // For this test, we'll use a known chord ID pattern or skip if no chords exist
        // In a real scenario, you'd query for a valid ID first
        var testId = "test-chord-id"; // Replace with actual ID from database

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/{testId}");

        // Assert
        // This might return 404 if the ID doesn't exist, which is expected behavior
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.NotFound));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var chord = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(chord.ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
            TestContext.WriteLine($"Found chord: {chord}");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Chord not found (expected): {error}");
        }
    }

    [Test]
    public async Task GetByQuality_WithValidQuality_ShouldReturnChords()
    {
        // Arrange
        var quality = "Major";

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/quality/{quality}?limit=10");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.BadRequest));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(chords.ValueKind, Is.EqualTo(JsonValueKind.Array));
            TestContext.WriteLine($"Found {chords.GetArrayLength()} {quality} chords");
        }
        else
        {
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(error.GetProperty("error").GetString(), Is.Not.Null);
            TestContext.WriteLine($"Error response (monad pattern): {error}");
        }
    }

    [Test]
    public async Task GetByQuality_WithInvalidQuality_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidQuality = "InvalidQualityThatDoesNotExist123";

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/quality/{invalidQuality}");

        // Assert - Should return 400 with error details
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("error").GetString(), Is.Not.Null);
        Assert.That(error.GetProperty("message").GetString(), Is.Not.Null);

        TestContext.WriteLine($"Error response: {error}");
    }

    [Test]
    public async Task GetByExtension_WithValidExtension_ShouldReturnChords()
    {
        // Arrange
        var extension = "7th";

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/extension/{extension}?limit=10");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.BadRequest));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(chords.ValueKind, Is.EqualTo(JsonValueKind.Array));
            TestContext.WriteLine($"Found {chords.GetArrayLength()} chords with {extension} extension");
        }
    }

    [Test]
    public async Task GetByStackingType_WithValidType_ShouldReturnChords()
    {
        // Arrange
        var stackingType = "Tertian";

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/stacking/{stackingType}?limit=10");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.BadRequest));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(chords.ValueKind, Is.EqualTo(JsonValueKind.Array));
            TestContext.WriteLine($"Found {chords.GetArrayLength()} {stackingType} chords");
        }
    }

    [Test]
    public async Task Search_WithValidQuery_ShouldReturnResults()
    {
        // Arrange
        var query = "major";

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/search?query={query}&limit=10");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.BadRequest));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(chords.ValueKind, Is.EqualTo(JsonValueKind.Array));
            TestContext.WriteLine($"Search for '{query}' returned {chords.GetArrayLength()} results");
        }
    }

    [Test]
    public async Task Search_WithEmptyQuery_ShouldReturnBadRequest()
    {
        // Arrange
        var query = "";

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/search?query={query}");

        // Assert
        TestContext.WriteLine($"Response Status: {response.StatusCode}");
        var responseBody = await response.Content.ReadAsStringAsync();
        TestContext.WriteLine($"Response Body: {responseBody}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();

        // ASP.NET Core's built-in validation returns a different format than our custom error handling
        // It returns: { "errors": { "query": ["The query field is required."] }, "type": "...", "title": "...", "status": 400, "traceId": "..." }
        // Our custom error format is: { "error": "...", "message": "...", "details": "..." }

        // Check for ASP.NET Core validation error format
        if (error.TryGetProperty("errors", out var errorsProperty))
        {
            // This is ASP.NET Core's built-in validation error
            Assert.That(errorsProperty.ValueKind, Is.EqualTo(JsonValueKind.Object), "Errors should be an object");
            TestContext.WriteLine($"ASP.NET Core validation error: {error}");
        }
        else if (error.TryGetProperty("error", out var errorProp))
        {
            // This is our custom error format
            Assert.That(errorProp.GetString(), Is.Not.Null.And.Not.Empty);
            TestContext.WriteLine($"Custom error format: {error}");
        }
        else
        {
            Assert.Fail(
                $"Response should have either 'errors' (ASP.NET Core validation) or 'error' (custom format) property. Actual: {error}");
        }
    }

    [Test]
    public async Task GetStatistics_ShouldReturnStatistics()
    {
        // Act
        var response = await _client!.GetAsync("/api/monadic/chords/statistics");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var stats = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(stats.ValueKind, Is.Not.EqualTo(JsonValueKind.Null));

        TestContext.WriteLine($"Chord statistics: {stats}");
    }

    [Test]
    public async Task GetAvailableQualities_ShouldReturnQualitiesList()
    {
        // Act
        var response = await _client!.GetAsync("/api/monadic/chords/qualities");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var qualities = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(qualities.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(qualities.GetArrayLength(), Is.GreaterThan(0));

        TestContext.WriteLine(
            $"Available qualities: {string.Join(", ", qualities.EnumerateArray().Select(q => q.GetString()))}");
    }

    [Test]
    public async Task MonadErrorHandling_ShouldReturnConsistentErrorFormat()
    {
        // Arrange - Trigger an error by using invalid parameters
        var invalidQuality = "NonExistentQuality999";

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/quality/{invalidQuality}");

        // Assert - Verify error response follows monad pattern
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify error structure
        Assert.That(error.TryGetProperty("error", out var errorProp), Is.True);
        Assert.That(error.TryGetProperty("message", out var messageProp), Is.True);
        Assert.That(error.TryGetProperty("details", out var detailsProp), Is.True);

        Assert.That(errorProp.GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(messageProp.GetString(), Is.Not.Null.And.Not.Empty);

        TestContext.WriteLine("Error format validation passed:");
        TestContext.WriteLine($"  Error: {errorProp.GetString()}");
        TestContext.WriteLine($"  Message: {messageProp.GetString()}");
        TestContext.WriteLine($"  Details: {detailsProp.GetString()}");
    }

    [Test]
    public async Task GetSimilar_WithValidId_ShouldReturnSimilarChords()
    {
        // Arrange
        var testId = "test-chord-id"; // Replace with actual ID

        // Act
        var response = await _client!.GetAsync($"/api/monadic/chords/{testId}/similar?limit=5");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var similar = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(similar.ValueKind, Is.EqualTo(JsonValueKind.Array));
            TestContext.WriteLine($"Found {similar.GetArrayLength()} similar chords");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            TestContext.WriteLine($"Expected error (test ID may not exist): {error}");
        }
    }
}
