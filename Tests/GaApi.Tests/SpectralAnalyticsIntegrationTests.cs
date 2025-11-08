namespace GaApi.Tests;

using System.Net;
using System.Net.Http.Json;
using GA.Business.Core.Analytics.Spectral;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

[TestFixture]
public class SpectralAnalyticsIntegrationTests
{
    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Test]
    public async Task AgentLoopEndpoint_ReturnsSpectralMetrics()
    {
        var request = new
        {
            agents = new[]
            {
                new
                {
                    id = "tier-1", displayName = "Tier 1", weight = 1.0,
                    signals = new Dictionary<string, double> { ["tier"] = 1.0 }
                },
                new
                {
                    id = "tier-2", displayName = "Tier 2", weight = 2.0,
                    signals = new Dictionary<string, double> { ["tier"] = 2.0 }
                },
                new
                {
                    id = "op-2-main", displayName = "Operation", weight = 0.6,
                    signals = new Dictionary<string, double> { ["tier"] = 2.0 }
                }
            },
            edges = new[]
            {
                new
                {
                    source = "tier-2", target = "tier-1", weight = 0.8,
                    features = new Dictionary<string, double> { ["dependency"] = 1.0 }
                },
                new
                {
                    source = "tier-2", target = "op-2-main", weight = 1.0,
                    features = new Dictionary<string, double> { ["operation"] = 1.0 }
                }
            },
            isUndirected = true,
            metadata = new Dictionary<string, string> { ["scenario"] = "integration" }
        };

        var response = await _client.PostAsJsonAsync("/api/spectral/agent-loop", request);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected HTTP 200 but received {(int)response.StatusCode}: {body}");
        }

        var metrics = await response.Content.ReadFromJsonAsync<AgentSpectralMetrics>();
        Assert.That(metrics, Is.Not.Null, "Spectral metrics payload should not be null");
        Assert.That(metrics!.Eigenvalues.Length, Is.GreaterThan(0));
        Assert.That(metrics.AlgebraicConnectivity, Is.GreaterThanOrEqualTo(0));
        Assert.That(metrics.SpectralRadius, Is.GreaterThan(0));
    }
}
