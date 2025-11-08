namespace GA.Business.Graphiti.Tests.Services;

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Graphiti.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Moq;
using Moq.Protected;
using NUnit.Framework;

[TestFixture]
public class GraphitiServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<GraphitiService>>();

        _options = Options.Create(new GraphitiOptions
        {
            BaseUrl = "http://localhost:8000",
            Timeout = TimeSpan.FromSeconds(30),
            MaxRetries = 3
        });

        _service = new GraphitiService(_httpClient, _options, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    private Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private HttpClient _httpClient;
    private Mock<ILogger<GraphitiService>> _loggerMock;
    private IOptions<GraphitiOptions> _options;
    private GraphitiService _service;

    [Test]
    public async Task AddEpisodeAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new EpisodeRequest
        {
            UserId = "test-user",
            EpisodeType = "practice",
            Content = new Dictionary<string, object>
            {
                ["chord_practiced"] = "Cmaj7",
                ["duration_minutes"] = 15,
                ["accuracy"] = 0.85
            }
        };

        var expectedResponse = new GraphitiResponse<object>
        {
            Status = "success",
            Message = "Episode added successfully"
        };

        var responseJson = JsonSerializer.Serialize(expectedResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.AddEpisodeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("success");
        result.Message.Should().Be("Episode added successfully");
    }

    [Test]
    public async Task SearchAsync_ValidRequest_ReturnsResults()
    {
        // Arrange
        var request = new SearchRequest
        {
            Query = "jazz chords",
            SearchType = "hybrid",
            Limit = 10,
            UserId = "test-user"
        };

        var expectedResponse = new SearchResponse
        {
            Status = "success",
            Query = "jazz chords",
            Results = new List<SearchResult>
            {
                new() { Content = "Cmaj7 is a jazz chord", Score = 0.95 },
                new() { Content = "Dm7 complements Cmaj7", Score = 0.87 }
            },
            Count = 2
        };

        var responseJson = JsonSerializer.Serialize(expectedResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.SearchAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("success");
        result.Query.Should().Be("jazz chords");
        result.Results.Should().HaveCount(2);
        result.Results[0].Content.Should().Be("Cmaj7 is a jazz chord");
        result.Results[0].Score.Should().Be(0.95);
    }

    [Test]
    public async Task GetRecommendationsAsync_ValidRequest_ReturnsRecommendations()
    {
        // Arrange
        var request = new RecommendationRequest
        {
            UserId = "test-user",
            RecommendationType = "next_chord",
            Context = new Dictionary<string, object>
            {
                ["current_skill_level"] = 3.5,
                ["recently_practiced"] = new[] { "C", "G", "Am" }
            }
        };

        var expectedResponse = new RecommendationResponse
        {
            Status = "success",
            UserId = "test-user",
            RecommendationType = "next_chord",
            Recommendations = new List<Recommendation>
            {
                new()
                {
                    Type = "next_chord",
                    Content = "Try learning Dm7",
                    Confidence = 0.92,
                    Reasoning = "Based on your jazz progression learning path"
                }
            }
        };

        var responseJson = JsonSerializer.Serialize(expectedResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.GetRecommendationsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("success");
        result.UserId.Should().Be("test-user");
        result.RecommendationType.Should().Be("next_chord");
        result.Recommendations.Should().HaveCount(1);
        result.Recommendations[0].Content.Should().Be("Try learning Dm7");
        result.Recommendations[0].Confidence.Should().Be(0.92);
    }

    [Test]
    public async Task GetUserProgressAsync_ValidUserId_ReturnsProgress()
    {
        // Arrange
        var userId = "test-user";
        var expectedResponse = new UserProgressResponse
        {
            Status = "success",
            UserId = userId,
            Progress = new UserProgress
            {
                SkillLevel = 3.5,
                SessionsCompleted = 12,
                RecentActivity = "Practiced Cmaj7 chord",
                ImprovementTrend = "positive",
                NextMilestone = "Master 7th chord progressions"
            }
        };

        var responseJson = JsonSerializer.Serialize(expectedResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.GetUserProgressAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("success");
        result.UserId.Should().Be(userId);
        result.Progress.Should().NotBeNull();
        result.Progress!.SkillLevel.Should().Be(3.5);
        result.Progress.SessionsCompleted.Should().Be(12);
        result.Progress.ImprovementTrend.Should().Be("positive");
    }

    [Test]
    public async Task IsHealthyAsync_ServiceHealthy_ReturnsTrue()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.IsHealthyAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task IsHealthyAsync_ServiceUnhealthy_ReturnsFalse()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task AddEpisodeAsync_HttpException_ReturnsErrorResponse()
    {
        // Arrange
        var request = new EpisodeRequest
        {
            UserId = "test-user",
            EpisodeType = "practice",
            Content = new Dictionary<string, object>()
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _service.AddEpisodeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("error");
        result.Message.Should().Contain("Network error");
    }
}
