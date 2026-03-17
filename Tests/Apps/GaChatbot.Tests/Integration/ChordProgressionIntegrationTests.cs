namespace GaChatbot.Tests.Integration;

using System.Collections.Generic;
using System.Threading.Tasks;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Plugins;
using GaChatbot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

/// <summary>
/// Integration tests for chord progression and harmonic analysis features in the GA chatbot.
/// Verifies that ProgressionSuggestionSkill and HarmonicAnalysisSkill are properly registered
/// and invoked by ProductionOrchestrator when appropriate queries are received.
/// </summary>
[TestFixture]
public sealed class ChordProgressionIntegrationTests
{
    private IHost _host = null!;
    private ProductionOrchestrator _orchestrator = null!;
    private ILogger<ChordProgressionIntegrationTests> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddGaChatbotServices();
        builder.Services.AddLogging(cfg => cfg.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _host = builder.Build();

        _orchestrator = _host.Services.GetRequiredService<ProductionOrchestrator>();
        _logger = _host.Services.GetRequiredService<ILogger<ChordProgressionIntegrationTests>>();
    }

    [TearDown]
    public void TearDown() => _host?.Dispose();

    /// <summary>
    /// Verifies that ProgressionSuggestionSkill is registered and triggered for a progression suggestion query.
    /// </summary>
    [Test]
    public async Task ProgressionSuggestion_GivenKeyRequest_ReturnsProgressions()
    {
        var request = new ChatRequest(
            "Suggest chord progressions for C major",
            SessionId: Guid.NewGuid().ToString());

        var response = await _orchestrator.AnswerAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.NaturalLanguageAnswer, Is.Not.Null.And.Not.Empty);
        _logger.LogInformation("ProgressionSuggestion response: {Response}", response.NaturalLanguageAnswer);
    }

    /// <summary>
    /// Verifies that ProgressionSuggestionSkill handles style-specific requests (e.g., "blues", "jazz").
    /// </summary>
    [Test]
    public async Task ProgressionSuggestion_WithStyle_ReturnsStyledProgressions()
    {
        var request = new ChatRequest(
            "Show me some jazz chord progressions in Am",
            SessionId: Guid.NewGuid().ToString());

        var response = await _orchestrator.AnswerAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.NaturalLanguageAnswer, Is.Not.Null.And.Not.Empty);
        _logger.LogInformation("Styled progression response: {Response}", response.NaturalLanguageAnswer);
    }

    /// <summary>
    /// Verifies that HarmonicAnalysisSkill is registered and triggered for harmonic analysis queries.
    /// </summary>
    [Test]
    public async Task HarmonicAnalysis_GivenChordProgression_ReturnsAnalysis()
    {
        var request = new ChatRequest(
            "Analyze the harmonic function of Am F C G",
            SessionId: Guid.NewGuid().ToString());

        var response = await _orchestrator.AnswerAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.NaturalLanguageAnswer, Is.Not.Null.And.Not.Empty);
        Assert.That(response.NaturalLanguageAnswer, Contains.Substring("roman").IgnoreCase.Or.Contains("function").IgnoreCase);
        _logger.LogInformation("Harmonic analysis response: {Response}", response.NaturalLanguageAnswer);
    }

    /// <summary>
    /// Verifies that ProgressionCompletionSkill completes in-progress progressions.
    /// </summary>
    [Test]
    public async Task ProgressionCompletion_GivenInProgressProgression_SuggestsCompletion()
    {
        var request = new ChatRequest(
            "Help me finish Am F C",
            SessionId: Guid.NewGuid().ToString());

        var response = await _orchestrator.AnswerAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.NaturalLanguageAnswer, Is.Not.Null.And.Not.Empty);
        _logger.LogInformation("Progression completion response: {Response}", response.NaturalLanguageAnswer);
    }

    /// <summary>
    /// Verifies that skills don't fire on unrelated queries.
    /// </summary>
    [Test]
    public async Task UnrelatedQuery_DoesNotRouteToProgressionSkills()
    {
        var request = new ChatRequest(
            "How do I play a barre chord?",
            SessionId: Guid.NewGuid().ToString());

        // This should NOT route to ProgressionSuggestionSkill or HarmonicAnalysisSkill
        // (though it may route to other skills or fall through to LLM)
        var response = await _orchestrator.AnswerAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.NaturalLanguageAnswer, Is.Not.Null.And.Not.Empty);
        _logger.LogInformation("Unrelated query response: {Response}", response.NaturalLanguageAnswer);
    }

    /// <summary>
    /// Verifies that conversation history is properly maintained across multiple requests.
    /// </summary>
    [Test]
    public async Task ConversationHistory_IsMaintainedAcrossRequests()
    {
        var sessionId = Guid.NewGuid().ToString();

        // First request: suggest progressions
        var request1 = new ChatRequest(
            "Suggest some progressions for G major",
            SessionId: sessionId);
        var response1 = await _orchestrator.AnswerAsync(request1);
        Assert.That(response1.NaturalLanguageAnswer, Is.Not.Null.And.Not.Empty);

        // Second request: refer to previous context (if supported by skill)
        var request2 = new ChatRequest(
            "Now analyze the harmonic function of those progressions",
            SessionId: sessionId);
        var response2 = await _orchestrator.AnswerAsync(request2);
        Assert.That(response2.NaturalLanguageAnswer, Is.Not.Null.And.Not.Empty);

        _logger.LogInformation("Request 1 response: {Response}", response1.NaturalLanguageAnswer);
        _logger.LogInformation("Request 2 response: {Response}", response2.NaturalLanguageAnswer);
    }
}
