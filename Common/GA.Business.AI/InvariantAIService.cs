namespace GA.Business.AI;

using System.Net.Http.Headers;
using System.Text.Json;
using Analytics.Analytics;
using Microsoft.Extensions.Options;

/// <summary>
///     AI-powered service for invariant analysis and recommendations
/// </summary>
public class InvariantAiService(
    ILogger<InvariantAiService> logger,
    InvariantAnalyticsService analyticsService,
    IOptions<AiConfiguration> config,
    HttpClient httpClient)
{
    private readonly AiConfiguration _config = config.Value;

    /// <summary>
    ///     Generate AI-powered recommendations for improving invariants
    /// </summary>
    public async Task<List<AiRecommendation>> GenerateRecommendationsAsync()
    {
        try
        {
            logger.LogInformation("Generating AI-powered invariant recommendations");

            var analytics = analyticsService.GetAllAnalytics();
            var violations = analyticsService.GetRecentViolations(1000);
            var insights = analyticsService.GetPerformanceInsights();

            var prompt = CreateAnalysisPrompt(analytics, violations, insights);
            var aiResponse = await CallAiServiceAsync(prompt);

            var recommendations = ParseAiRecommendations(aiResponse);

            logger.LogInformation("Generated {Count} AI recommendations", recommendations.Count);
            return recommendations;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating AI recommendations");
            return [];
        }
    }

    /// <summary>
    ///     Analyze data quality issues using AI
    /// </summary>
    public async Task<DataQualityAnalysis> AnalyzeDataQualityAsync(string conceptType)
    {
        try
        {
            logger.LogInformation("Analyzing data quality for concept type: {ConceptType}", conceptType);

            var analytics = analyticsService.GetAnalyticsByConceptType(conceptType);
            var violations = analyticsService.GetRecentViolations(500)
                .Where(v => v.ConceptType.Equals(conceptType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var prompt = CreateDataQualityPrompt(conceptType, analytics, violations);
            var aiResponse = await CallAiServiceAsync(prompt);

            var analysis = ParseDataQualityAnalysis(aiResponse, conceptType);

            logger.LogInformation("Completed data quality analysis for {ConceptType}", conceptType);
            return analysis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing data quality for {ConceptType}", conceptType);
            return new DataQualityAnalysis { ConceptType = conceptType, OverallScore = 0.5 };
        }
    }

    /// <summary>
    ///     Suggest new invariants based on data patterns
    /// </summary>
    public async Task<List<InvariantSuggestion>> SuggestNewInvariantsAsync(string conceptType)
    {
        try
        {
            logger.LogInformation("Generating invariant suggestions for concept type: {ConceptType}", conceptType);

            var analytics = analyticsService.GetAnalyticsByConceptType(conceptType);
            var violations = analyticsService.GetRecentViolations(1000)
                .Where(v => v.ConceptType.Equals(conceptType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Analyze violation patterns
            var patterns = AnalyzeViolationPatterns(violations);

            var prompt = CreateInvariantSuggestionPrompt(conceptType, analytics, patterns);
            var aiResponse = await CallAiServiceAsync(prompt);

            var suggestions = ParseInvariantSuggestions(aiResponse, conceptType);

            logger.LogInformation("Generated {Count} invariant suggestions for {ConceptType}", suggestions.Count,
                conceptType);
            return suggestions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating invariant suggestions for {ConceptType}", conceptType);
            return [];
        }
    }

    /// <summary>
    ///     Predict potential validation failures
    /// </summary>
    public Task<List<ValidationPrediction>> PredictValidationFailuresAsync()
    {
        try
        {
            logger.LogInformation("Predicting potential validation failures");

            var analytics = analyticsService.GetAllAnalytics();
            var trends = analyticsService.GetViolationTrends(TimeSpan.FromDays(7));

            var predictions = new List<ValidationPrediction>();

            // Simple ML-like analysis based on trends
            foreach (var analytic in analytics.Where(a => a.FailureRate > 0))
            {
                var riskScore = CalculateRiskScore(analytic, trends);

                if (riskScore > 0.7)
                {
                    predictions.Add(new ValidationPrediction
                    {
                        InvariantName = analytic.InvariantName,
                        ConceptType = analytic.ConceptType,
                        RiskScore = riskScore,
                        PredictedFailureRate = Math.Min(analytic.FailureRate * 1.5, 1.0),
                        Confidence = CalculateConfidence(analytic),
                        RecommendedActions = GenerateRecommendedActions(analytic, riskScore)
                    });
                }
            }

            logger.LogInformation("Generated {Count} validation failure predictions", predictions.Count);
            return Task.FromResult(predictions.OrderByDescending(p => p.RiskScore).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error predicting validation failures");
            return Task.FromResult(new List<ValidationPrediction>());
        }
    }

    /// <summary>
    ///     Optimize invariant configuration using AI
    /// </summary>
    public Task<InvariantOptimization> OptimizeInvariantConfigurationAsync()
    {
        try
        {
            logger.LogInformation("Optimizing invariant configuration");

            var analytics = analyticsService.GetAllAnalytics();
            var insights = analyticsService.GetPerformanceInsights();

            var optimization = new InvariantOptimization
            {
                OptimizedAt = DateTime.UtcNow,
                CurrentPerformance = insights,
                Recommendations = []
            };

            // Performance optimizations
            var slowInvariants = analytics.Where(a => a.AverageExecutionTime > TimeSpan.FromMilliseconds(100)).ToList();
            foreach (var invariant in slowInvariants)
            {
                optimization.Recommendations.Add(new OptimizationRecommendation
                {
                    Type = OptimizationType.Performance,
                    InvariantName = invariant.InvariantName,
                    ConceptType = invariant.ConceptType,
                    Description =
                        $"Consider optimizing {invariant.InvariantName} - current avg execution time: {invariant.AverageExecutionTime.TotalMilliseconds:F1}ms",
                    ExpectedImprovement = "30-50% performance improvement",
                    Priority = invariant.AverageExecutionTime > TimeSpan.FromMilliseconds(200)
                        ? OptimizationPriority.High
                        : OptimizationPriority.Medium
                });
            }

            // Accuracy optimizations
            var failingInvariants = analytics.Where(a => a.FailureRate > 0.2).ToList();
            foreach (var invariant in failingInvariants)
            {
                optimization.Recommendations.Add(new OptimizationRecommendation
                {
                    Type = OptimizationType.Accuracy,
                    InvariantName = invariant.InvariantName,
                    ConceptType = invariant.ConceptType,
                    Description =
                        $"Review {invariant.InvariantName} validation logic - current failure rate: {invariant.FailureRate:P}",
                    ExpectedImprovement = "Improved data quality and reduced false positives",
                    Priority = invariant.FailureRate > 0.5 ? OptimizationPriority.High : OptimizationPriority.Medium
                });
            }

            logger.LogInformation("Generated {Count} optimization recommendations", optimization.Recommendations.Count);
            return Task.FromResult(optimization);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error optimizing invariant configuration");
            return Task.FromResult(new InvariantOptimization { OptimizedAt = DateTime.UtcNow });
        }
    }

    private string CreateAnalysisPrompt(List<InvariantAnalytics> analytics, List<ViolationEvent> violations,
        PerformanceInsights insights)
    {
        return $@"
Analyze the following invariant validation data and provide recommendations:

PERFORMANCE INSIGHTS:
- Total Validations: {insights.TotalValidations}
- Total Failures: {insights.TotalFailures}
- Overall Success Rate: {insights.OverallSuccessRate:P}
- Average Execution Time: {insights.AverageExecutionTime.TotalMilliseconds:F1}ms

TOP FAILING INVARIANTS:
{string.Join("\n", analytics.Where(a => a.FailureRate > 0).Take(5).Select(a =>
    $"- {a.InvariantName} ({a.ConceptType}): {a.FailureRate:P} failure rate, {a.FailedValidations} failures"))}

RECENT VIOLATION PATTERNS:
{string.Join("\n", violations.Take(10).Select(v =>
    $"- {v.InvariantName}: {v.ErrorMessage}"))}

Please provide specific, actionable recommendations for improving data quality and validation performance.
";
    }

    private string CreateDataQualityPrompt(string conceptType, List<InvariantAnalytics> analytics,
        List<ViolationEvent> violations)
    {
        return $@"
Analyze data quality for {conceptType} based on validation results:

INVARIANT PERFORMANCE:
{string.Join("\n", analytics.Select(a =>
    $"- {a.InvariantName}: {a.SuccessRate:P} success rate, {a.TotalValidations} validations"))}

COMMON VIOLATIONS:
{string.Join("\n", violations.GroupBy(v => v.ErrorMessage).Take(5).Select(g =>
    $"- {g.Key}: {g.Count()} occurrences"))}

Provide a data quality assessment and specific improvement recommendations.
";
    }

    private string CreateInvariantSuggestionPrompt(string conceptType, List<InvariantAnalytics> analytics,
        Dictionary<string, int> patterns)
    {
        return $@"
Suggest new invariants for {conceptType} based on current validation patterns:

EXISTING INVARIANTS:
{string.Join("\n", analytics.Select(a => $"- {a.InvariantName}"))}

VIOLATION PATTERNS:
{string.Join("\n", patterns.Select(p => $"- {p.Key}: {p.Value} occurrences"))}

Suggest 3-5 new invariants that could improve data quality for {conceptType}.
";
    }

    private async Task<string> CallAiServiceAsync(string prompt)
    {
        if (!_config.EnableAi || string.IsNullOrEmpty(_config.ApiEndpoint))
        {
            return "AI service not configured";
        }

        try
        {
            var request = new
            {
                prompt,
                max_tokens = _config.MaxTokens,
                temperature = _config.Temperature
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            }

            var response = await httpClient.PostAsync(_config.ApiEndpoint, content);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling AI service");
            return "Error calling AI service";
        }
    }

    private static List<AiRecommendation> ParseAiRecommendations(string aiResponse)
    {
        // Simple parsing - in production, use more sophisticated NLP
        var recommendations = new List<AiRecommendation>();

        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Where(l => l.StartsWith('-') || l.StartsWith('*')))
        {
            recommendations.Add(new AiRecommendation
            {
                Title = line.Trim('-', '*', ' '),
                Description = line.Trim('-', '*', ' '),
                Confidence = 0.8,
                Category = "General"
            });
        }

        return recommendations;
    }

    private static DataQualityAnalysis ParseDataQualityAnalysis(string aiResponse, string conceptType)
    {
        // Simple scoring based on response content
        var score = aiResponse.ToLowerInvariant().Contains("good") ? 0.8 :
            aiResponse.ToLowerInvariant().Contains("poor") ? 0.3 : 0.6;

        return new DataQualityAnalysis
        {
            ConceptType = conceptType,
            OverallScore = score,
            AnalyzedAt = DateTime.UtcNow,
            Summary = aiResponse.Length > 200 ? aiResponse[..200] + "..." : aiResponse,
            Issues = ExtractIssues(aiResponse),
            Recommendations = ExtractRecommendations(aiResponse)
        };
    }

    private static List<InvariantSuggestion> ParseInvariantSuggestions(string aiResponse, string conceptType)
    {
        var suggestions = new List<InvariantSuggestion>();
        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines.Where(l => l.Contains("invariant", StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add(new InvariantSuggestion
            {
                Name = ExtractInvariantName(line),
                ConceptType = conceptType,
                Description = line.Trim(),
                Confidence = 0.7,
                EstimatedImpact = "Medium"
            });
        }

        return suggestions;
    }

    private static Dictionary<string, int> AnalyzeViolationPatterns(List<ViolationEvent> violations)
    {
        return violations
            .GroupBy(v => v.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static double CalculateRiskScore(InvariantAnalytics analytic, ViolationTrends trends)
    {
        var baseRisk = analytic.FailureRate;
        var trendMultiplier =
            trends.ViolationsByInvariant.GetValueOrDefault(analytic.InvariantName, 0) > 10 ? 1.5 : 1.0;
        var performanceMultiplier = analytic.AverageExecutionTime > TimeSpan.FromMilliseconds(100) ? 1.2 : 1.0;

        return Math.Min(baseRisk * trendMultiplier * performanceMultiplier, 1.0);
    }

    private static double CalculateConfidence(InvariantAnalytics analytic)
    {
        return analytic.TotalValidations > 100 ? 0.9 :
            analytic.TotalValidations > 50 ? 0.7 : 0.5;
    }

    private static List<string> GenerateRecommendedActions(InvariantAnalytics analytic, double riskScore)
    {
        var actions = new List<string>();

        if (riskScore > 0.8)
        {
            actions.Add("Review invariant logic immediately");
            actions.Add("Check data quality for this concept type");
        }
        else if (riskScore > 0.6)
        {
            actions.Add("Monitor closely for trend changes");
            actions.Add("Consider adjusting validation criteria");
        }

        return actions;
    }

    private static List<string> ExtractIssues(string text)
    {
        return text.Split('\n')
            .Where(line => line.ToLowerInvariant().Contains("issue") || line.ToLowerInvariant().Contains("problem"))
            .Take(5)
            .ToList();
    }

    private static List<string> ExtractRecommendations(string text)
    {
        return text.Split('\n')
            .Where(line => line.ToLowerInvariant().Contains("recommend") || line.ToLowerInvariant().Contains("suggest"))
            .Take(5)
            .ToList();
    }

    private static string ExtractInvariantName(string line)
    {
        // Simple extraction - look for quoted text or capitalize words
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", words.Where(w => char.IsUpper(w[0])).Take(3));
    }
}
