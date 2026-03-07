namespace GaApi.Tests;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NUnit.Framework;
using GaApi.Models;
using GaApi.Services;
using System.Collections.Generic;
using AllProjects.ServiceDefaults;

[TestFixture]
public class SearchIntegrationTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Replace the service with a custom implementation for testing
                    services.RemoveAll<VectorSearchService>();
                    services.AddSingleton<VectorSearchService, FakeVectorSearchService>();
                    
                    // Also disable the background initialization service to speed up tests
                    var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(VoicingIndexInitializationService));
                    if (descriptor != null) services.Remove(descriptor);
                });
            });

        _client = _factory.CreateClient();
    }

    private class FakeVectorSearchService : VectorSearchService
    {
        public FakeVectorSearchService() : base(null!, null!, null!, null!) { }

        public override Task<List<ChordSearchResult>> HybridSearchAsync(
            string query, string? quality = null, string? extension = null, 
            string? stackingType = null, int? noteCount = null, int limit = 10, int numCandidates = 100)
        {
            return Task.FromResult(new List<ChordSearchResult>
            {
                new ChordSearchResult { Id = 1, Name = "C Major", Score = 0.95 }
            });
        }
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task HybridSearch_ReturnsSuccessAndResults()
    {
        var request = new
        {
            Query = "jazz chords",
            Limit = 5
        };

        var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/search/hybrid", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<ChordSearchResult>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(results, Is.Not.Null);
        Assert.That(results!.Count, Is.GreaterThan(0));
        Assert.That(results[0].Name, Is.EqualTo("C Major"));
    }

    [Test]
    public async Task HybridSearch_EmptyQuery_ReturnsBadRequest()
    {
        var request = new
        {
            Query = "",
            Limit = 5
        };

        var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/search/hybrid", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
