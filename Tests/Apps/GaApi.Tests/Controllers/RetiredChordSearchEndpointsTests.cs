namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Pins the retirement of GaApi's chord vector-search endpoints (POST /api/search/hybrid,
///     GET /api/stats, GET /stats). Voicing search is served by POST /api/voicings/retrieve.
/// </summary>
[TestFixture]
[Category("Integration")]
public class RetiredChordSearchEndpointsTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new TestWebApplicationFactory();
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
    public async Task HybridSearch_IsRetired()
    {
        var response = await _client!.PostAsJsonAsync("/api/search/hybrid", new { query = "major seventh chords" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("/api/stats")]
    [TestCase("/stats")]
    public async Task ChordIndexStats_AreRetired(string path)
    {
        var response = await _client!.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
