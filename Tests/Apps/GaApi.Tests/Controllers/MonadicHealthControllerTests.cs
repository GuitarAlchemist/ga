namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Integration tests for MonadicHealthController demonstrating Try monad error handling
/// </summary>
[TestFixture]
[Category("Integration")]
public class MonadicHealthControllerTests
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
    public async Task GetHealth_ShouldReturnHealthStatus()
    {
        // Act
        var response = await _client!.GetAsync("/api/monadic/health");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));

        var health = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify health response structure
        Assert.That(health.TryGetProperty("status", out var statusProp), Is.True);
        Assert.That(health.TryGetProperty("version", out var versionProp), Is.True);
        Assert.That(health.TryGetProperty("environment", out var envProp), Is.True);
        Assert.That(health.TryGetProperty("services", out var servicesProp), Is.True);

        var status = statusProp.GetString();
        Assert.That(status, Is.AnyOf("Healthy", "Degraded", "Unhealthy"));

        TestContext.WriteLine($"Overall Status: {status}");
        TestContext.WriteLine($"Version: {versionProp.GetString()}");
        TestContext.WriteLine($"Environment: {envProp.GetString()}");
        TestContext.WriteLine($"Services: {servicesProp}");
    }

    [Test]
    public async Task CheckDatabase_ShouldReturnDatabaseHealth()
    {
        // Act
        var response = await _client!.GetAsync("/api/monadic/health/database");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));

        var dbHealth = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify database health structure
        Assert.That(dbHealth.TryGetProperty("status", out var statusProp), Is.True);

        var status = statusProp.GetString();
        Assert.That(status, Is.AnyOf("Healthy", "Unhealthy"));

        if (status == "Healthy")
        {
            TestContext.WriteLine("Database is healthy");
            if (dbHealth.TryGetProperty("responseTime", out var responseTime))
            {
                TestContext.WriteLine($"Response time: {responseTime.GetDouble()}ms");
            }
        }
        else
        {
            Assert.That(dbHealth.TryGetProperty("error", out var errorProp), Is.True);
            TestContext.WriteLine($"Database unhealthy: {errorProp.GetString()}");
        }
    }

    [Test]
    public async Task CheckVectorSearch_ShouldReturnVectorSearchHealth()
    {
        // Act
        var response = await _client!.GetAsync("/api/monadic/health/vector-search");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));

        var vsHealth = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify vector search health structure
        Assert.That(vsHealth.TryGetProperty("status", out var statusProp), Is.True);

        var status = statusProp.GetString();
        Assert.That(status, Is.AnyOf("Healthy", "Unhealthy"));

        TestContext.WriteLine($"Vector Search Status: {status}");

        if (status == "Unhealthy" && vsHealth.TryGetProperty("error", out var errorProp))
        {
            TestContext.WriteLine($"Error: {errorProp.GetString()}");
        }
    }

    [Test]
    public async Task CheckMemoryCache_ShouldReturnCacheHealth()
    {
        // Act
        var response = await _client!.GetAsync("/api/monadic/health/cache");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));

        var cacheHealth = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify cache health structure
        Assert.That(cacheHealth.TryGetProperty("status", out var statusProp), Is.True);

        var status = statusProp.GetString();
        Assert.That(status, Is.AnyOf("Healthy", "Unhealthy"));

        TestContext.WriteLine($"Memory Cache Status: {status}");

        if (status == "Unhealthy" && cacheHealth.TryGetProperty("error", out var errorProp))
        {
            TestContext.WriteLine($"Error: {errorProp.GetString()}");
        }
    }

    [Test]
    public async Task GetDetailedHealth_ShouldReturnAllServiceChecks()
    {
        // Act
        var response = await _client!.GetAsync("/api/monadic/health/detailed");

        // Assert
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));

        var detailedHealth = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify detailed health structure
        Assert.That(detailedHealth.TryGetProperty("timestamp", out var timestampProp), Is.True);
        Assert.That(detailedHealth.TryGetProperty("overallStatus", out var overallStatusProp), Is.True);
        Assert.That(detailedHealth.TryGetProperty("database", out var dbProp), Is.True);
        Assert.That(detailedHealth.TryGetProperty("vectorSearch", out var vsProp), Is.True);
        Assert.That(detailedHealth.TryGetProperty("memoryCache", out var cacheProp), Is.True);

        var overallStatus = overallStatusProp.GetString();
        Assert.That(overallStatus, Is.AnyOf("Healthy", "Degraded"));

        TestContext.WriteLine("Detailed Health Report:");
        TestContext.WriteLine($"  Timestamp: {timestampProp.GetDateTime()}");
        TestContext.WriteLine($"  Overall Status: {overallStatus}");
        TestContext.WriteLine($"  Database: {dbProp.GetProperty("status").GetString()}");
        TestContext.WriteLine($"  Vector Search: {vsProp.GetProperty("status").GetString()}");
        TestContext.WriteLine($"  Memory Cache: {cacheProp.GetProperty("status").GetString()}");
    }

    [Test]
    public async Task TryMonadErrorHandling_ShouldReturnConsistentFormat()
    {
        // This test verifies that all health endpoints return consistent error formats
        // when using the Try monad pattern

        // Act - Get all health endpoints
        var endpoints = new[]
        {
            "/api/monadic/health",
            "/api/monadic/health/database",
            "/api/monadic/health/vector-search",
            "/api/monadic/health/cache",
            "/api/monadic/health/detailed"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await _client!.GetAsync(endpoint);

            // Assert - All should return either 200 or 503
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable),
                $"Endpoint {endpoint} returned unexpected status code");

            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty,
                $"Endpoint {endpoint} returned empty response");

            // Verify it's valid JSON
            var json = JsonDocument.Parse(content);
            Assert.That(json.RootElement.ValueKind, Is.Not.EqualTo(JsonValueKind.Null),
                $"Endpoint {endpoint} returned null JSON");

            TestContext.WriteLine($"{endpoint}: {response.StatusCode}");
        }
    }

    [Test]
    public async Task HealthEndpoints_ShouldHandleServiceFailuresGracefully()
    {
        // This test verifies that health checks handle service failures gracefully
        // using the Try monad pattern

        // Act
        var response = await _client!.GetAsync("/api/monadic/health");

        // Assert - Should always return a response, even if services are down
        Assert.That(response, Is.Not.Null);
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));

        var health = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Should always have a status field
        Assert.That(health.TryGetProperty("status", out var statusProp), Is.True);
        Assert.That(statusProp.GetString(), Is.Not.Null.And.Not.Empty);

        // Should always have services field
        Assert.That(health.TryGetProperty("services", out var servicesProp), Is.True);

        // If any service is unhealthy, it should have an error message
        foreach (var service in servicesProp.EnumerateObject())
        {
            var serviceHealth = service.Value;
            var serviceStatus = serviceHealth.GetProperty("status").GetString();

            if (serviceStatus == "Unhealthy")
            {
                Assert.That(serviceHealth.TryGetProperty("error", out var errorProp), Is.True,
                    $"Service {service.Name} is unhealthy but has no error message");
                Assert.That(errorProp.GetString(), Is.Not.Null.And.Not.Empty,
                    $"Service {service.Name} has empty error message");

                TestContext.WriteLine($"Service {service.Name} is unhealthy: {errorProp.GetString()}");
            }
            else
            {
                TestContext.WriteLine($"Service {service.Name} is {serviceStatus}");
            }
        }
    }

    [Test]
    public async Task DetailedHealth_ShouldComposeMultipleTryMonads()
    {
        // This test verifies that the detailed health endpoint correctly composes
        // multiple Try monad results

        // Act
        var response = await _client!.GetAsync("/api/monadic/health/detailed");

        // Assert
        var detailedHealth = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Extract individual service statuses
        var dbStatus = detailedHealth.GetProperty("database").GetProperty("status").GetString();
        var vsStatus = detailedHealth.GetProperty("vectorSearch").GetProperty("status").GetString();
        var cacheStatus = detailedHealth.GetProperty("memoryCache").GetProperty("status").GetString();
        var overallStatus = detailedHealth.GetProperty("overallStatus").GetString();

        // Verify overall status is correctly computed from individual statuses
        var allHealthy = dbStatus == "Healthy" && vsStatus == "Healthy" && cacheStatus == "Healthy";

        if (allHealthy)
        {
            Assert.That(overallStatus, Is.EqualTo("Healthy"),
                "Overall status should be Healthy when all services are healthy");
        }
        else
        {
            Assert.That(overallStatus, Is.EqualTo("Degraded"),
                "Overall status should be Degraded when any service is unhealthy");
        }

        TestContext.WriteLine("Monad composition verification:");
        TestContext.WriteLine($"  Database: {dbStatus}");
        TestContext.WriteLine($"  Vector Search: {vsStatus}");
        TestContext.WriteLine($"  Memory Cache: {cacheStatus}");
        TestContext.WriteLine($"  Overall (composed): {overallStatus}");
        TestContext.WriteLine(
            $"  Composition is correct: {(allHealthy && overallStatus == "Healthy") || (!allHealthy && overallStatus == "Degraded")}");
    }
}
