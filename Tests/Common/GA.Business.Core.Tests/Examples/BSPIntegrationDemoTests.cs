namespace GA.Business.Core.Tests.Examples;

// using GA.Business.Core.Examples; // Namespace does not exist

#if false // TODO: Fix missing types: BSPIntegrationDemo, BSPEnhancedChordAnalysisService, BSPEnhancedProgressionAnalysisService
[TestFixture]
public class BSPIntegrationDemoTests
{
    private ServiceProvider _serviceProvider;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TonalBSP:MaxTreeDepth"] = "4",
                ["TonalBSP:MinElementsPerLeaf"] = "5",
                ["TonalBSP:EnableCaching"] = "true",
                ["TonalBSP:EnableDetailedLogging"] = "false"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Add Tonal BSP services
        services.AddTonalBSP(configuration);

        services.AddScoped<BSPIntegrationDemo>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task BSPIntegrationDemo_ShouldRunWithoutErrors()
    {
        // Arrange
        var demo = _serviceProvider.GetRequiredService<BSPIntegrationDemo>();

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await demo.RunDemo());
    }

    [Test]
    public async Task BSPIntegrationDemo_StaticMethod_ShouldRunWithoutErrors()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await BSPIntegrationDemo.RunDemoAsync());
    }

    [Test]
    public void BSPServices_ShouldBeRegisteredCorrectly()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<BSPIntegrationDemo>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<TonalBSPService>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<TonalBSPAnalyzer>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<BSPEnhancedChordAnalysisService>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<BSPEnhancedProgressionAnalysisService>());
    }
}
#endif
