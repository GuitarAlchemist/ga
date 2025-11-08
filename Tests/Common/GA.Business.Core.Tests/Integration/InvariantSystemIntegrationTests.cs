namespace GA.Business.Core.Tests.Integration;

using Business.AI.AI;
using Core.Analytics;
using Core.Invariants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
// using GA.Business.Core.Services; // Namespace does not exist

[TestFixture]
public class InvariantSystemIntegrationTests
{
    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        // Add memory cache
        services.AddMemoryCache();

        // Add HTTP client for AI service
        // services.AddHttpClient<InvariantAIService>(); // Requires Microsoft.Extensions.Http package

        // Configure AI (disabled for tests)
        services.Configure<AiConfiguration>(options => { options.EnableAi = false; });

        // Add invariant validation services
        services.AddInvariantValidation(options =>
        {
            options.EnableCaching = true;
            options.EnablePerformanceMonitoring = true;
            options.EnableAsyncValidation = false; // Synchronous for testing
        });

        // Add analytics and AI services
        services.AddSingleton<InvariantAnalyticsService>();
        services.AddScoped<InvariantAiService>();

        _serviceProvider = services.BuildServiceProvider();

        // Get services
        _validationService = _serviceProvider.GetRequiredService<InvariantValidationService>();
        _analyticsService = _serviceProvider.GetRequiredService<InvariantAnalyticsService>();
        _aiService = _serviceProvider.GetRequiredService<InvariantAiService>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    private ServiceProvider _serviceProvider;
    private InvariantValidationService _validationService;
    private InvariantAnalyticsService _analyticsService;
    private InvariantAiService _aiService;

    [Test]
    public async Task FullWorkflow_ValidateAndAnalyze_ShouldWorkEndToEnd()
    {
        // Arrange
        var validChord = new IconicChordDefinition
        {
            Name = "C Major",
            TheoreticalName = "Cmaj",
            PitchClasses = [0, 4, 7],
            GuitarVoicing = [-1, 3, 2, 0, 1, 0],
            Artist = "Various",
            Song = "Many Songs",
            Genre = "Classical",
            Era = "Classical Period"
        };

        var invalidChord = new IconicChordDefinition
        {
            Name = "", // Invalid
            TheoreticalName = "Cmaj",
            PitchClasses = [0], // Invalid - too few notes
            Artist = "",
            Song = "",
            Genre = "InvalidGenre",
            Era = "1960s"
        };

        // Act - Validate multiple chords to generate analytics data
        var validResult = _validationService.ValidateIconicChord(validChord);
        var invalidResult1 = _validationService.ValidateIconicChord(invalidChord);
        var invalidResult2 = _validationService.ValidateIconicChord(invalidChord);
        var invalidResult3 = _validationService.ValidateIconicChord(invalidChord);

        // Wait a moment for analytics to be recorded
        await Task.Delay(100);

        // Get analytics
        var allAnalytics = _analyticsService.GetAllAnalytics();
        var topFailing = _analyticsService.GetTopFailingInvariants(5);
        var insights = _analyticsService.GetPerformanceInsights();
        var recommendations = _analyticsService.GetRecommendations();

        // Generate AI recommendations
        var aiRecommendations = await _aiService.GenerateRecommendationsAsync();
        var dataQualityAnalysis = await _aiService.AnalyzeDataQualityAsync("IconicChordDefinition");
        var predictions = await _aiService.PredictValidationFailuresAsync();

        // Assert validation results
        Assert.That(validResult.IsValid, Is.True);
        Assert.That(invalidResult1.IsValid, Is.False);
        Assert.That(invalidResult1.Failures.Count(), Is.GreaterThan(0));

        // Assert analytics
        Assert.That(allAnalytics.Count, Is.GreaterThan(0));
        Assert.That(insights.TotalValidations, Is.GreaterThan(0));
        Assert.That(insights.TotalFailures, Is.GreaterThan(0));
        Assert.That(topFailing.Count, Is.GreaterThan(0));

        // Assert recommendations
        Assert.That(recommendations.Count, Is.GreaterThan(0));
        var highFailureRecs = recommendations.Where(r => r.Type == RecommendationType.HighFailureRate).ToList();
        Assert.That(highFailureRecs.Count, Is.GreaterThan(0));

        // Assert AI integration (even with AI disabled, should return empty results gracefully)
        Assert.That(aiRecommendations, Is.Not.Null);
        Assert.That(dataQualityAnalysis, Is.Not.Null);
        Assert.That(predictions, Is.Not.Null);
    }

    [Test]
    public void ServiceRegistration_AllServicesResolved_ShouldNotThrow()
    {
        // Act & Assert - All services should resolve without throwing
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<InvariantValidationService>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<InvariantAnalyticsService>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<InvariantAiService>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<InvariantFactoryRegistry>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<InvariantValidationPerformanceMonitor>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<ConfigurationBroadcastService>());
    }

    [Test]
    public async Task PerformanceTest_ManyValidations_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var chord = new IconicChordDefinition
        {
            Name = "Test Chord",
            TheoreticalName = "Cmaj",
            PitchClasses = [0, 4, 7],
            Artist = "Test Artist",
            Song = "Test Song",
            Genre = "Rock",
            Era = "1960s"
        };

        var stopwatch = Stopwatch.StartNew();

        // Act - Perform many validations
        var tasks = new List<Task<CompositeInvariantValidationResult>>();
        for (var i = 0; i < 100; i++)
        {
            // Create slight variations to avoid caching
            var testChord = new IconicChordDefinition
            {
                Name = $"Test Chord {i}",
                TheoreticalName = "Cmaj",
                PitchClasses = [0, 4, 7],
                Artist = "Test Artist",
                Song = "Test Song",
                Genre = "Rock",
                Era = "1960s"
            };

            // For this test, we'll run synchronously to measure actual validation time
            var result = _validationService.ValidateIconicChord(testChord);
            Assert.That(result, Is.Not.Null);
        }

        stopwatch.Stop();

        // Assert - Should complete in reasonable time (less than 5 seconds for 100 validations)
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000),
            $"100 validations took {stopwatch.ElapsedMilliseconds}ms, which is too slow");

        // Verify analytics were recorded
        var insights = _analyticsService.GetPerformanceInsights();
        Assert.That(insights.TotalValidations, Is.GreaterThanOrEqualTo(100));
    }

    [Test]
    public void AnalyticsAccuracy_MultipleValidations_ShouldTrackCorrectly()
    {
        // Arrange
        var validChord = new IconicChordDefinition
        {
            Name = "Valid Chord",
            TheoreticalName = "Cmaj",
            PitchClasses = [0, 4, 7],
            Artist = "Artist",
            Song = "Song",
            Genre = "Rock",
            Era = "1960s"
        };

        var invalidChord = new IconicChordDefinition
        {
            Name = "", // Invalid
            TheoreticalName = "Cmaj",
            PitchClasses = [0, 4, 7],
            Artist = "Artist",
            Song = "Song",
            Genre = "Rock",
            Era = "1960s"
        };

        // Clear any existing analytics
        _analyticsService.ClearAnalytics();

        // Act - Perform known number of validations
        var validResults = 0;
        var invalidResults = 0;

        for (var i = 0; i < 7; i++)
        {
            var result = _validationService.ValidateIconicChord(validChord);
            if (result.IsValid)
            {
                validResults++;
            }
        }

        for (var i = 0; i < 3; i++)
        {
            var result = _validationService.ValidateIconicChord(invalidChord);
            if (!result.IsValid)
            {
                invalidResults++;
            }
        }

        // Assert analytics accuracy
        var insights = _analyticsService.GetPerformanceInsights();
        Assert.That(insights.TotalValidations,
            Is.GreaterThanOrEqualTo(10)); // At least 10 individual invariant validations

        // Check that we have both successful and failed validations recorded
        Assert.That(insights.TotalFailures, Is.GreaterThan(0));
        Assert.That(insights.OverallSuccessRate, Is.LessThan(1.0)); // Should be less than 100% due to invalid chord
    }

    [Test]
    public async Task ErrorHandling_InvalidData_ShouldNotCrashSystem()
    {
        // Arrange - Create chord with null/invalid data
        var problematicChord = new IconicChordDefinition
        {
            Name = null!, // Null name
            TheoreticalName = null!,
            PitchClasses = null!, // Null collection
            Artist = null!,
            Song = null!,
            Genre = null!,
            Era = null!
        };

        // Act & Assert - Should not throw exceptions
        Assert.DoesNotThrow(() =>
        {
            var result = _validationService.ValidateIconicChord(problematicChord);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.False);
        });

        // Analytics should still work
        Assert.DoesNotThrow(() =>
        {
            var analytics = _analyticsService.GetAllAnalytics();
            var insights = _analyticsService.GetPerformanceInsights();
            Assert.That(analytics, Is.Not.Null);
            Assert.That(insights, Is.Not.Null);
        });

        // AI services should handle errors gracefully
        Assert.DoesNotThrowAsync(async () =>
        {
            var recommendations = await _aiService.GenerateRecommendationsAsync();
            var analysis = await _aiService.AnalyzeDataQualityAsync("IconicChordDefinition");
            Assert.That(recommendations, Is.Not.Null);
            Assert.That(analysis, Is.Not.Null);
        });
    }

    [Test]
    public void ConcurrentAccess_MultipleThreads_ShouldBeThreadSafe()
    {
        // Arrange
        var chord = new IconicChordDefinition
        {
            Name = "Concurrent Test",
            TheoreticalName = "Cmaj",
            PitchClasses = [0, 4, 7],
            Artist = "Artist",
            Song = "Song",
            Genre = "Rock",
            Era = "1960s"
        };

        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // Act - Run validations concurrently
        for (var i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < 10; j++)
                    {
                        var testChord = new IconicChordDefinition
                        {
                            Name = $"Concurrent Test {taskId}-{j}",
                            TheoreticalName = "Cmaj",
                            PitchClasses = [0, 4, 7],
                            Artist = "Artist",
                            Song = "Song",
                            Genre = "Rock",
                            Era = "1960s"
                        };

                        var result = _validationService.ValidateIconicChord(testChord);
                        Assert.That(result, Is.Not.Null);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - No exceptions should occur
        Assert.That(exceptions.Count, Is.EqualTo(0),
            $"Concurrent access caused {exceptions.Count} exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");

        // Analytics should have recorded all validations
        var insights = _analyticsService.GetPerformanceInsights();
        Assert.That(insights.TotalValidations, Is.GreaterThan(0));
    }
}
