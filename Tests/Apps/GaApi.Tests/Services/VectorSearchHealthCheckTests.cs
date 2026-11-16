namespace GaApi.Tests.Services;

using GA.Business.ML.Search;
using GaApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable SKEXP0001 // CpuVoicingSearchStrategy is marked experimental; GaApi suppresses it the same way

/// <summary>
///     Pins the vector-search health probe to the layer-4 voicing search strategy
///     (<see cref="IVoicingSearchStrategy" />) after the retirement of GaApi's chord vector-search stack.
/// </summary>
[TestFixture]
public class VectorSearchHealthCheckTests
{
    private static readonly IConfiguration EmptyConfiguration = new ConfigurationBuilder().Build();

    [Test]
    public async Task HealthCheckService_ReportsVoicingSearchStrategyType()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new HealthCheckService(
            null!, // the vector-search probe does not touch MongoDB
            new CpuVoicingSearchStrategy(),
            cache,
            NullLogger<HealthCheckService>.Instance,
            EmptyConfiguration);

        var health = await service.CheckVectorSearchAsync();

        Assert.That(health.Status, Is.EqualTo("Healthy"));
        Assert.That(health.Details!["StrategyType"], Is.EqualTo(nameof(CpuVoicingSearchStrategy)));
    }

    [Test]
    public async Task MonadicHealthCheckService_ReportsVoicingSearchStrategyType()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new MonadicHealthCheckService(
            null!, // the vector-search probe does not touch MongoDB
            new CpuVoicingSearchStrategy(),
            cache,
            NullLogger<MonadicHealthCheckService>.Instance,
            EmptyConfiguration);

        var health = (await service.CheckVectorSearchAsync()).GetValueOrThrow();

        Assert.That(health.Status, Is.EqualTo("Healthy"));
        Assert.That(health.Details!["StrategyType"], Is.EqualTo(nameof(CpuVoicingSearchStrategy)));
    }
}
