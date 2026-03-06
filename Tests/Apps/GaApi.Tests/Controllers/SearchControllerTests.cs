namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Integration tests for <see cref="GaApi.Controllers.SearchController" />.
///     Covers the POST /api/search/hybrid endpoint.
///     The vector search service is backed by the in-memory strategy when Ollama/MongoDB
///     are unavailable, so results may be empty — but the HTTP contract is always verified.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SearchControllerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new();
        _client  = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program>? _factory;
    private HttpClient?                     _client;

    // ── POST /api/search/hybrid ─────────────────────────────────────────────────

    [Test]
    public async Task HybridSearch_ShouldReturn200OrHandleNoEmbeddingService_WithValidQuery()
    {
        var request = new { query = "dark jazzy chords" };
        var response = await _client!.PostAsJsonAsync("/api/search/hybrid", request);

        // 200 when an embedding service is configured; 500 when neither OpenAI nor local embeddings
        // are available (expected in offline CI environments).
        Assert.That(response.StatusCode,
            Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task HybridSearch_ShouldReturnJsonArray_WhenEmbeddingServiceAvailable()
    {
        var request  = new { query = "open voicing guitar chords" };
        var response = await _client!.PostAsJsonAsync("/api/search/hybrid", request);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            Assert.Ignore("Embedding service not available in this environment — skipping body assertion.");
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    [Test]
    public async Task HybridSearch_ShouldReturn400_WhenQueryIsEmpty()
    {
        var request = new { query = "" };
        var response = await _client!.PostAsJsonAsync("/api/search/hybrid", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task HybridSearch_ShouldReturn400_WhenQueryIsWhitespace()
    {
        var request = new { query = "   " };
        var response = await _client!.PostAsJsonAsync("/api/search/hybrid", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task HybridSearch_ShouldReturn400_WhenQueryIsMissing()
    {
        // Sends a body with no "query" field at all (null after deserialization)
        var json     = """{"limit":5}""";
        var content  = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client!.PostAsync("/api/search/hybrid", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task HybridSearch_ShouldRespectLimit_WhenSpecified()
    {
        var request  = new { query = "minor chords", limit = 3 };
        var response = await _client!.PostAsJsonAsync("/api/search/hybrid", request);

        // The service may return fewer results than requested if the index is small,
        // but must never exceed the requested limit.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(results.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(results.GetArrayLength(), Is.LessThanOrEqualTo(3));
    }

    [Test]
    public async Task HybridSearch_ResultsShouldHaveExpectedShape_WhenResultsReturned()
    {
        var request  = new { query = "major seventh chords", limit = 5 };
        var response = await _client!.PostAsJsonAsync("/api/search/hybrid", request);
        var results  = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Only validate shape if any results were returned (index may be empty in CI)
        foreach (var result in results.EnumerateArray())
        {
            Assert.That(result.TryGetProperty("name",      out _), Is.True, "missing name");
            Assert.That(result.TryGetProperty("quality",   out _), Is.True, "missing quality");
            Assert.That(result.TryGetProperty("score",     out _), Is.True, "missing score");
        }
    }

    [Test]
    public async Task HybridSearch_ShouldReturn200_WithOptionalFilters()
    {
        var request = new
        {
            query        = "major",
            quality      = "Major",
            noteCount    = 4,
            limit        = 5,
            numCandidates = 20
        };
        var response = await _client!.PostAsJsonAsync("/api/search/hybrid", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
