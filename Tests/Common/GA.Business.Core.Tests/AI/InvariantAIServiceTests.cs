namespace GA.Business.Core.Tests.AI;

using System.Net;
using System.Net.Http;
using System.Threading;
using Business.AI;
using Business.Analytics.Analytics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

[TestFixture]
public class InvariantAiServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<InvariantAiService>>();
        _mockAnalyticsService = new Mock<InvariantAnalyticsService>(
            Mock.Of<ILogger<InvariantAnalyticsService>>(),
            Mock.Of<IMemoryCache>());

        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);

        _config = new AiConfiguration
        {
            EnableAi = true,
            ApiEndpoint = "https://api.example.com/ai",
            ApiKey = "test-key",
            MaxTokens = 1000,
            Temperature = 0.7
        };

        var options = Options.Create(_config);
        _aiService = new InvariantAiService(_mockLogger.Object, _mockAnalyticsService.Object, options, _httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
    }

    private InvariantAiService _aiService = null!;
    private Mock<ILogger<InvariantAiService>> _mockLogger = null!;
    private Mock<InvariantAnalyticsService> _mockAnalyticsService = null!;
    private Mock<HttpMessageHandler> _mockHttpHandler = null!;
    private HttpClient _httpClient = null!;
    private AiConfiguration _config = null!;

    [Test]
    public async Task GenerateRecommendationsAsync_WithValidData_ShouldReturnRecommendations()
    {
        // Arrange
        var analytics = new List<InvariantAnalytics>
        {
            new() { InvariantName = "TestInvariant", FailureRate = 0.3, TotalValidations = 100 }
        };
        var violations = new List<ViolationEvent>
        {
            new() { InvariantName = "TestInvariant", ErrorMessage = "Test error" }
        };
        var insights = new PerformanceInsights { TotalValidations = 100, TotalFailures = 30 };

        _mockAnalyticsService.Setup(x => x.GetAllAnalytics()).Returns(analytics);
        _mockAnalyticsService.Setup(x => x.GetRecentViolations(1000)).Returns(violations);
        _mockAnalyticsService.Setup(x => x.GetPerformanceInsights()).Returns(insights);

        // Mock HTTP response - ParseAIRecommendations looks for lines starting with '-' or '*'
        var responseContent = "- Improve validation logic for TestInvariant\n- Review data quality";
        SetupHttpResponse(responseContent);

        // Act
        var recommendations = await _aiService.GenerateRecommendationsAsync();

        // Assert
        Assert.That(recommendations, Is.Not.Null);
        Assert.That(recommendations.Count, Is.GreaterThan(0));
        // The parser strips the '-' and spaces, so check for the actual content
        Assert.That(recommendations[0].Title, Does.Contain("Improve validation logic") | Does.Contain("Review data quality"));
    }

    [Test]
    public async Task AnalyzeDataQualityAsync_WithValidConceptType_ShouldReturnAnalysis()
    {
        // Arrange
        var conceptType = "IconicChord";
        var analytics = new List<InvariantAnalytics>
        {
            new() { InvariantName = "NameNotEmpty", ConceptType = conceptType, SuccessRate = 0.95 }
        };
        var violations = new List<ViolationEvent>
        {
            new() { ConceptType = conceptType, ErrorMessage = "Name cannot be empty" }
        };

        _mockAnalyticsService.Setup(x => x.GetAnalyticsByConceptType(conceptType)).Returns(analytics);
        _mockAnalyticsService.Setup(x => x.GetRecentViolations(500)).Returns(violations);

        var responseContent = "Data quality is good overall with minor issues in name validation";
        SetupHttpResponse(responseContent);

        // Act
        var analysis = await _aiService.AnalyzeDataQualityAsync(conceptType);

        // Assert
        Assert.That(analysis, Is.Not.Null);
        Assert.That(analysis.ConceptType, Is.EqualTo(conceptType));
        Assert.That(analysis.OverallScore, Is.GreaterThan(0));
        Assert.That(analysis.Summary, Is.Not.Empty);
    }

    [Test]
    public async Task SuggestNewInvariantsAsync_WithValidData_ShouldReturnSuggestions()
    {
        // Arrange
        var conceptType = "ChordProgression";
        var analytics = new List<InvariantAnalytics>
        {
            new() { InvariantName = "NameNotEmpty", ConceptType = conceptType }
        };
        var violations = new List<ViolationEvent>
        {
            new() { ConceptType = conceptType, ErrorMessage = "Invalid chord sequence" }
        };

        _mockAnalyticsService.Setup(x => x.GetAnalyticsByConceptType(conceptType)).Returns(analytics);
        _mockAnalyticsService.Setup(x => x.GetRecentViolations(1000)).Returns(violations);

        var responseContent = "Suggest ChordSequenceValid invariant to validate chord progressions";
        SetupHttpResponse(responseContent);

        // Act
        var suggestions = await _aiService.SuggestNewInvariantsAsync(conceptType);

        // Assert
        Assert.That(suggestions, Is.Not.Null);
        Assert.That(suggestions.Count, Is.GreaterThan(0));
        Assert.That(suggestions[0].ConceptType, Is.EqualTo(conceptType));
    }

    [Test]
    public async Task PredictValidationFailuresAsync_WithTrendData_ShouldReturnPredictions()
    {
        // Arrange
        // The implementation calculates riskScore = failureRate * trendMultiplier * performanceMultiplier
        // trendMultiplier = 1.5 if violations > 10, else 1.0
        // performanceMultiplier = 1.2 if avgTime > 100ms, else 1.0
        // Only returns predictions if riskScore > 0.7
        // So: 0.4 * 1.5 * 1.0 = 0.6 (not > 0.7)
        // Need failureRate = 0.5 to get: 0.5 * 1.5 * 1.0 = 0.75 > 0.7
        var analytics = new List<InvariantAnalytics>
        {
            new()
            {
                InvariantName = "HighRiskInvariant",
                ConceptType = "TestType",
                FailureRate = 0.5, // Increased from 0.4 to 0.5
                TotalValidations = 100,
                AverageExecutionTime = TimeSpan.FromMilliseconds(50)
            }
        };
        var trends = new ViolationTrends
        {
            ViolationsByInvariant = new Dictionary<string, int> { ["HighRiskInvariant"] = 15 }
        };

        _mockAnalyticsService.Setup(x => x.GetAllAnalytics()).Returns(analytics);
        _mockAnalyticsService.Setup(x => x.GetViolationTrends(It.IsAny<TimeSpan>())).Returns(trends);

        // Act
        var predictions = await _aiService.PredictValidationFailuresAsync();

        // Assert
        Assert.That(predictions, Is.Not.Null);
        Assert.That(predictions.Count, Is.GreaterThan(0));
        Assert.That(predictions[0].InvariantName, Is.EqualTo("HighRiskInvariant"));
        Assert.That(predictions[0].RiskScore, Is.GreaterThan(0.7));
    }

    [Test]
    public async Task OptimizeInvariantConfigurationAsync_ShouldReturnOptimizations()
    {
        // Arrange
        var analytics = new List<InvariantAnalytics>
        {
            new()
            {
                InvariantName = "SlowInvariant",
                ConceptType = "TestType",
                AverageExecutionTime = TimeSpan.FromMilliseconds(150),
                FailureRate = 0.1
            },
            new()
            {
                InvariantName = "FailingInvariant",
                ConceptType = "TestType",
                AverageExecutionTime = TimeSpan.FromMilliseconds(20),
                FailureRate = 0.6
            }
        };
        var insights = new PerformanceInsights();

        _mockAnalyticsService.Setup(x => x.GetAllAnalytics()).Returns(analytics);
        _mockAnalyticsService.Setup(x => x.GetPerformanceInsights()).Returns(insights);

        // Act
        var optimization = await _aiService.OptimizeInvariantConfigurationAsync();

        // Assert
        Assert.That(optimization, Is.Not.Null);
        Assert.That(optimization.Recommendations.Count, Is.GreaterThan(0));

        var performanceRec = optimization.Recommendations.FirstOrDefault(r => r.Type == OptimizationType.Performance);
        var accuracyRec = optimization.Recommendations.FirstOrDefault(r => r.Type == OptimizationType.Accuracy);

        Assert.That(performanceRec, Is.Not.Null);
        Assert.That(performanceRec!.InvariantName, Is.EqualTo("SlowInvariant"));

        Assert.That(accuracyRec, Is.Not.Null);
        Assert.That(accuracyRec!.InvariantName, Is.EqualTo("FailingInvariant"));
    }

    [Test]
    public async Task GenerateRecommendationsAsync_WithAIDisabled_ShouldReturnEmptyList()
    {
        // Arrange
        _config.EnableAi = false;

        // Act
        var recommendations = await _aiService.GenerateRecommendationsAsync();

        // Assert
        Assert.That(recommendations, Is.Not.Null);
        Assert.That(recommendations.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GenerateRecommendationsAsync_WithHttpError_ShouldHandleGracefully()
    {
        // Arrange
        _mockAnalyticsService.Setup(x => x.GetAllAnalytics()).Returns([]);
        _mockAnalyticsService.Setup(x => x.GetRecentViolations(1000)).Returns([]);
        _mockAnalyticsService.Setup(x => x.GetPerformanceInsights()).Returns(new PerformanceInsights());

        // Setup HTTP error
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var recommendations = await _aiService.GenerateRecommendationsAsync();

        // Assert
        Assert.That(recommendations, Is.Not.Null);
        Assert.That(recommendations.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task AnalyzeDataQualityAsync_WithException_ShouldReturnDefaultAnalysis()
    {
        // Arrange
        var conceptType = "TestType";
        _mockAnalyticsService.Setup(x => x.GetAnalyticsByConceptType(conceptType))
            .Throws(new Exception("Test exception"));

        // Act
        var analysis = await _aiService.AnalyzeDataQualityAsync(conceptType);

        // Assert
        Assert.That(analysis, Is.Not.Null);
        Assert.That(analysis.ConceptType, Is.EqualTo(conceptType));
        Assert.That(analysis.OverallScore, Is.EqualTo(0.5)); // Default score
    }

    private void SetupHttpResponse(string content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"choices\":[{{\"text\":\"{content}\"}}]}}")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}
