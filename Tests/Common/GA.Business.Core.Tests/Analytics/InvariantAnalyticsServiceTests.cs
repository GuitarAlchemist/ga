namespace GA.Business.Core.Tests.Analytics;

using Business.Analytics.Analytics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

[TestFixture]
public class InvariantAnalyticsServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<InvariantAnalyticsService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _analyticsService = new InvariantAnalyticsService(_mockLogger.Object, _memoryCache);
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
    }

    private InvariantAnalyticsService _analyticsService;
    private Mock<ILogger<InvariantAnalyticsService>> _mockLogger;
    private IMemoryCache _memoryCache;

    [Test]
    public void RecordValidation_ValidInput_ShouldRecordMetrics()
    {
        // Arrange
        var invariantName = "TestInvariant";
        var conceptType = "TestConcept";
        var executionTime = TimeSpan.FromMilliseconds(50);

        // Act
        _analyticsService.RecordValidation(invariantName, conceptType, true, executionTime);

        // Assert
        var analytics = _analyticsService.GetInvariantAnalytics(invariantName, conceptType);
        Assert.That(analytics, Is.Not.Null);
        Assert.That(analytics!.TotalValidations, Is.EqualTo(1));
        Assert.That(analytics.SuccessfulValidations, Is.EqualTo(1));
        Assert.That(analytics.FailedValidations, Is.EqualTo(0));
        Assert.That(analytics.SuccessRate, Is.EqualTo(1.0));
        Assert.That(analytics.FailureRate, Is.EqualTo(0.0));
    }

    [Test]
    public void RecordValidation_FailedValidation_ShouldRecordFailure()
    {
        // Arrange
        var invariantName = "TestInvariant";
        var conceptType = "TestConcept";
        var executionTime = TimeSpan.FromMilliseconds(30);
        var errorMessage = "Test error";

        // Act
        _analyticsService.RecordValidation(invariantName, conceptType, false, executionTime, errorMessage);

        // Assert
        var analytics = _analyticsService.GetInvariantAnalytics(invariantName, conceptType);
        Assert.That(analytics, Is.Not.Null);
        Assert.That(analytics!.TotalValidations, Is.EqualTo(1));
        Assert.That(analytics.SuccessfulValidations, Is.EqualTo(0));
        Assert.That(analytics.FailedValidations, Is.EqualTo(1));
        Assert.That(analytics.SuccessRate, Is.EqualTo(0.0));
        Assert.That(analytics.FailureRate, Is.EqualTo(1.0));

        var recentViolations = _analyticsService.GetRecentViolations(10);
        Assert.That(recentViolations.Count, Is.EqualTo(1));
        Assert.That(recentViolations[0].ErrorMessage, Is.EqualTo(errorMessage));
    }

    [Test]
    public void RecordValidation_MultipleValidations_ShouldAggregateCorrectly()
    {
        // Arrange
        var invariantName = "TestInvariant";
        var conceptType = "TestConcept";

        // Act - Record multiple validations
        _analyticsService.RecordValidation(invariantName, conceptType, true, TimeSpan.FromMilliseconds(10));
        _analyticsService.RecordValidation(invariantName, conceptType, false, TimeSpan.FromMilliseconds(20), "Error 1");
        _analyticsService.RecordValidation(invariantName, conceptType, true, TimeSpan.FromMilliseconds(30));
        _analyticsService.RecordValidation(invariantName, conceptType, false, TimeSpan.FromMilliseconds(40), "Error 2");

        // Assert
        var analytics = _analyticsService.GetInvariantAnalytics(invariantName, conceptType);
        Assert.That(analytics, Is.Not.Null);
        Assert.That(analytics!.TotalValidations, Is.EqualTo(4));
        Assert.That(analytics.SuccessfulValidations, Is.EqualTo(2));
        Assert.That(analytics.FailedValidations, Is.EqualTo(2));
        Assert.That(analytics.SuccessRate, Is.EqualTo(0.5));
        Assert.That(analytics.FailureRate, Is.EqualTo(0.5));
        Assert.That(analytics.MinExecutionTime, Is.EqualTo(TimeSpan.FromMilliseconds(10)));
        Assert.That(analytics.MaxExecutionTime, Is.EqualTo(TimeSpan.FromMilliseconds(40)));
    }

    [Test]
    public void GetTopFailingInvariants_ShouldReturnOrderedByFailureRate()
    {
        // Arrange
        _analyticsService.RecordValidation("Invariant1", "Type1", false, TimeSpan.FromMilliseconds(10), "Error");
        _analyticsService.RecordValidation("Invariant1", "Type1", false, TimeSpan.FromMilliseconds(10), "Error");
        _analyticsService.RecordValidation("Invariant1", "Type1", true, TimeSpan.FromMilliseconds(10));

        _analyticsService.RecordValidation("Invariant2", "Type1", false, TimeSpan.FromMilliseconds(10), "Error");
        _analyticsService.RecordValidation("Invariant2", "Type1", true, TimeSpan.FromMilliseconds(10));
        _analyticsService.RecordValidation("Invariant2", "Type1", true, TimeSpan.FromMilliseconds(10));

        // Act
        var topFailing = _analyticsService.GetTopFailingInvariants(5);

        // Assert
        Assert.That(topFailing.Count, Is.EqualTo(2));
        Assert.That(topFailing[0].InvariantName, Is.EqualTo("Invariant1")); // 66% failure rate
        Assert.That(topFailing[1].InvariantName, Is.EqualTo("Invariant2")); // 33% failure rate
        Assert.That(topFailing[0].FailureRate, Is.GreaterThan(topFailing[1].FailureRate));
    }

    [Test]
    public void GetSlowestInvariants_ShouldReturnOrderedByExecutionTime()
    {
        // Arrange
        _analyticsService.RecordValidation("FastInvariant", "Type1", true, TimeSpan.FromMilliseconds(10));
        _analyticsService.RecordValidation("SlowInvariant", "Type1", true, TimeSpan.FromMilliseconds(100));
        _analyticsService.RecordValidation("MediumInvariant", "Type1", true, TimeSpan.FromMilliseconds(50));

        // Act
        var slowest = _analyticsService.GetSlowestInvariants(5);

        // Assert
        Assert.That(slowest.Count, Is.EqualTo(3));
        Assert.That(slowest[0].InvariantName, Is.EqualTo("SlowInvariant"));
        Assert.That(slowest[1].InvariantName, Is.EqualTo("MediumInvariant"));
        Assert.That(slowest[2].InvariantName, Is.EqualTo("FastInvariant"));
    }

    [Test]
    public void GetViolationTrends_ShouldAnalyzeTrendsCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _analyticsService.RecordValidation("Invariant1", "Type1", false, TimeSpan.FromMilliseconds(10), "Error1");
        _analyticsService.RecordValidation("Invariant1", "Type1", false, TimeSpan.FromMilliseconds(10), "Error1");
        _analyticsService.RecordValidation("Invariant2", "Type2", false, TimeSpan.FromMilliseconds(10), "Error2");

        // Act
        var trends = _analyticsService.GetViolationTrends(TimeSpan.FromHours(1));

        // Assert
        Assert.That(trends.TotalViolations, Is.EqualTo(3));
        Assert.That(trends.ViolationsByInvariant["Invariant1"], Is.EqualTo(2));
        Assert.That(trends.ViolationsByInvariant["Invariant2"], Is.EqualTo(1));
        Assert.That(trends.ViolationsByConceptType["Type1"], Is.EqualTo(2));
        Assert.That(trends.ViolationsByConceptType["Type2"], Is.EqualTo(1));
    }

    [Test]
    public void GetPerformanceInsights_ShouldCalculateCorrectly()
    {
        // Arrange
        _analyticsService.RecordValidation("Invariant1", "Type1", true, TimeSpan.FromMilliseconds(10));
        _analyticsService.RecordValidation("Invariant1", "Type1", false, TimeSpan.FromMilliseconds(20), "Error");
        _analyticsService.RecordValidation("Invariant2", "Type1", true, TimeSpan.FromMilliseconds(100));

        // Act
        var insights = _analyticsService.GetPerformanceInsights();

        // Assert
        Assert.That(insights.TotalValidations, Is.EqualTo(3));
        Assert.That(insights.TotalFailures, Is.EqualTo(1));
        Assert.That(insights.OverallSuccessRate, Is.EqualTo(2.0 / 3.0).Within(0.01));
        Assert.That(insights.SlowestInvariant, Is.EqualTo("Invariant2"));
        Assert.That(insights.MostFailedInvariant, Is.EqualTo("Invariant1"));
    }

    [Test]
    public void GetRecommendations_ShouldGenerateAppropriateRecommendations()
    {
        // Arrange - Create high failure rate scenario
        for (var i = 0; i < 15; i++)
        {
            _analyticsService.RecordValidation("HighFailureInvariant", "Type1", false, TimeSpan.FromMilliseconds(10),
                "Error");
        }

        for (var i = 0; i < 5; i++)
        {
            _analyticsService.RecordValidation("HighFailureInvariant", "Type1", true, TimeSpan.FromMilliseconds(10));
        }

        // Create slow execution scenario
        _analyticsService.RecordValidation("SlowInvariant", "Type1", true, TimeSpan.FromMilliseconds(300));

        // Act
        var recommendations = _analyticsService.GetRecommendations();

        // Assert
        Assert.That(recommendations.Count, Is.GreaterThan(0));

        var highFailureRec = recommendations.FirstOrDefault(r => r.Type == RecommendationType.HighFailureRate);
        Assert.That(highFailureRec, Is.Not.Null);
        Assert.That(highFailureRec!.InvariantName, Is.EqualTo("HighFailureInvariant"));

        var slowExecRec = recommendations.FirstOrDefault(r => r.Type == RecommendationType.SlowExecution);
        Assert.That(slowExecRec, Is.Not.Null);
        Assert.That(slowExecRec!.InvariantName, Is.EqualTo("SlowInvariant"));
    }

    [Test]
    public void ClearAnalytics_ShouldRemoveAllData()
    {
        // Arrange
        _analyticsService.RecordValidation("TestInvariant", "TestType", true, TimeSpan.FromMilliseconds(10));
        _analyticsService.RecordValidation("TestInvariant", "TestType", false, TimeSpan.FromMilliseconds(10), "Error");

        // Verify data exists
        var analyticsBefore = _analyticsService.GetAllAnalytics();
        var violationsBefore = _analyticsService.GetRecentViolations(10);
        Assert.That(analyticsBefore.Count, Is.GreaterThan(0));
        Assert.That(violationsBefore.Count, Is.GreaterThan(0));

        // Act
        _analyticsService.ClearAnalytics();

        // Assert
        var analyticsAfter = _analyticsService.GetAllAnalytics();
        var violationsAfter = _analyticsService.GetRecentViolations(10);
        Assert.That(analyticsAfter.Count, Is.EqualTo(0));
        Assert.That(violationsAfter.Count, Is.EqualTo(0));
    }

    [Test]
    public void ExportAnalytics_ShouldIncludeAllData()
    {
        // Arrange
        _analyticsService.RecordValidation("TestInvariant", "TestType", true, TimeSpan.FromMilliseconds(10));
        _analyticsService.RecordValidation("TestInvariant", "TestType", false, TimeSpan.FromMilliseconds(20), "Error");

        // Act
        var export = _analyticsService.ExportAnalytics();

        // Assert
        Assert.That(export.ExportedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(export.Metrics.Count, Is.GreaterThan(0));
        Assert.That(export.ViolationEvents.Count, Is.GreaterThan(0));
        Assert.That(export.PerformanceInsights, Is.Not.Null);
        Assert.That(export.Recommendations, Is.Not.Null);
    }

    [Test]
    public void GetAnalyticsByConceptType_ShouldFilterCorrectly()
    {
        // Arrange
        _analyticsService.RecordValidation("Invariant1", "Type1", true, TimeSpan.FromMilliseconds(10));
        _analyticsService.RecordValidation("Invariant2", "Type2", true, TimeSpan.FromMilliseconds(10));
        _analyticsService.RecordValidation("Invariant3", "Type1", true, TimeSpan.FromMilliseconds(10));

        // Act
        var type1Analytics = _analyticsService.GetAnalyticsByConceptType("Type1");
        var type2Analytics = _analyticsService.GetAnalyticsByConceptType("Type2");

        // Assert
        Assert.That(type1Analytics.Count, Is.EqualTo(2));
        Assert.That(type2Analytics.Count, Is.EqualTo(1));
        Assert.That(type1Analytics.All(a => a.ConceptType == "Type1"), Is.True);
        Assert.That(type2Analytics.All(a => a.ConceptType == "Type2"), Is.True);
    }
}
