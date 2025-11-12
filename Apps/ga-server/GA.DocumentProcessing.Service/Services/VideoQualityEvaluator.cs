namespace GA.DocumentProcessing.Service.Services;

using Models;

/// <summary>
/// Evaluates YouTube videos for quality, relevance, and educational value
/// Uses Ollama LLM to analyze video metadata and make intelligent decisions
/// </summary>
public class VideoQualityEvaluator
{
    private readonly ILogger<VideoQualityEvaluator> _logger;
    private readonly OllamaSummarizationService _ollamaService;
    private readonly YouTubeTranscriptService _transcriptService;

    public VideoQualityEvaluator(
        ILogger<VideoQualityEvaluator> logger,
        OllamaSummarizationService ollamaService,
        YouTubeTranscriptService transcriptService)
    {
        _logger = logger;
        _ollamaService = ollamaService;
        _transcriptService = transcriptService;
    }

    /// <summary>
    /// Evaluate a YouTube video for quality and relevance
    /// </summary>
    public async Task<VideoEvaluation> EvaluateVideoAsync(
        YouTubeSearchResult video,
        KnowledgeGap gap,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Evaluating video: {VideoId} - {Title}", video.VideoId, video.Title);

        var evaluation = new VideoEvaluation
        {
            VideoId = video.VideoId,
            Title = video.Title,
            Description = video.Description,
            ChannelName = video.ChannelName,
            ViewCount = (int)video.ViewCount,
            Duration = video.Duration,
            PublishedAt = video.PublishedAt
        };

        try
        {
            // Step 1: Calculate engagement score (views/likes ratio, recency)
            evaluation.EngagementScore = CalculateEngagementScore(video);

            // Step 2: Use Ollama to evaluate relevance and educational value
            await EvaluateWithOllamaAsync(video, gap, evaluation, cancellationToken);

            // Step 3: Calculate overall quality score
            evaluation.QualityScore = CalculateOverallQualityScore(evaluation);

            // Step 4: Determine recommended action
            evaluation.RecommendedAction = DetermineRecommendedAction(evaluation);

            _logger.LogInformation(
                "Evaluation complete for {VideoId}: Quality={Quality:F2}, Relevance={Relevance:F2}, Educational={Educational:F2}",
                video.VideoId,
                evaluation.QualityScore,
                evaluation.RelevanceScore,
                evaluation.EducationalValueScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating video {VideoId}", video.VideoId);
            evaluation.QualityScore = 0;
            evaluation.RecommendedAction = "Reject";
            evaluation.EvaluationReasoning = $"Evaluation failed: {ex.Message}";
        }

        return evaluation;
    }

    /// <summary>
    /// Calculate engagement score based on views, recency, and duration
    /// </summary>
    private double CalculateEngagementScore(YouTubeSearchResult video)
    {
        var score = 0.0;

        // View count score (logarithmic scale)
        if (video.ViewCount > 0)
        {
            var viewScore = Math.Min(1.0, Math.Log10(video.ViewCount) / 7.0); // 10M views = 1.0
            score += viewScore * 0.4;
        }

        // Recency score (newer is better)
        var daysSincePublished = (DateTime.UtcNow - video.PublishedAt).TotalDays;
        var recencyScore = Math.Max(0, 1.0 - (daysSincePublished / 365.0)); // 1 year old = 0.0
        score += recencyScore * 0.3;

        // Duration score (prefer 5-20 minute videos)
        var durationMinutes = video.Duration.TotalMinutes;
        var durationScore = durationMinutes switch
        {
            < 3 => 0.3,  // Too short
            >= 3 and < 5 => 0.6,
            >= 5 and <= 20 => 1.0,  // Ideal length
            > 20 and <= 40 => 0.7,
            _ => 0.4  // Too long
        };
        score += durationScore * 0.3;

        return Math.Min(1.0, score);
    }

    /// <summary>
    /// Use Ollama to evaluate relevance and educational value
    /// </summary>
    private async Task EvaluateWithOllamaAsync(
        YouTubeSearchResult video,
        KnowledgeGap gap,
        VideoEvaluation evaluation,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to get transcript for deeper analysis
            string? transcriptPreview = null;
            try
            {
                var transcript = await _transcriptService.ExtractTranscriptAsync(video.Url, cancellationToken);
                if (transcript?.Segments.Any() == true)
                {
                    // Get first 500 words of transcript
                    var words = string.Join(" ", transcript.Segments.Take(50).Select(s => s.Text));
                    transcriptPreview = words.Length > 500 ? words.Substring(0, 500) + "..." : words;
                }
            }
            catch
            {
                // Transcript extraction failed, continue with title/description only
            }

            var prompt = $@"You are an expert music educator evaluating YouTube videos for a guitar learning platform.

**Knowledge Gap to Fill:**
Category: {gap.Category}
Topic: {gap.Topic}
Description: {gap.Description}

**Video to Evaluate:**
Title: {video.Title}
Channel: {video.ChannelName}
Description: {video.Description}
Duration: {video.Duration.TotalMinutes:F1} minutes
Views: {video.ViewCount:N0}
Published: {video.PublishedAt:yyyy-MM-dd}
{(transcriptPreview != null ? $"Transcript Preview: {transcriptPreview}" : "")}

**Evaluation Criteria:**
1. **Relevance** (0-1): How well does this video address the specific knowledge gap?
2. **Educational Value** (0-1): How effective is this video for teaching the topic?
3. **Quality Indicators**: Clear explanations, good audio/video, structured content, practical examples

**Respond with ONLY a JSON object:**
{{
  ""relevance_score"": 0.0-1.0,
  ""educational_value_score"": 0.0-1.0,
  ""reasoning"": ""brief explanation of scores"",
  ""positive_factors"": [""factor1"", ""factor2""],
  ""negative_factors"": [""factor1"", ""factor2""]
}}";

            var response = await _ollamaService.GenerateTextAsync(prompt, cancellationToken);

            // Parse JSON response
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(response, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                var json = System.Text.Json.JsonDocument.Parse(jsonMatch.Value);
                var root = json.RootElement;

                if (root.TryGetProperty("relevance_score", out var relevance))
                {
                    evaluation.RelevanceScore = relevance.GetDouble();
                }

                if (root.TryGetProperty("educational_value_score", out var educational))
                {
                    evaluation.EducationalValueScore = educational.GetDouble();
                }

                if (root.TryGetProperty("reasoning", out var reasoning))
                {
                    evaluation.EvaluationReasoning = reasoning.GetString() ?? string.Empty;
                }
            }
            else
            {
                // Fallback: use simple heuristics
                evaluation.RelevanceScore = CalculateSimpleRelevance(video, gap);
                evaluation.EducationalValueScore = 0.5; // Neutral
                evaluation.EvaluationReasoning = "Ollama evaluation failed, using heuristics";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama evaluation failed for {VideoId}, using fallback", video.VideoId);
            evaluation.RelevanceScore = CalculateSimpleRelevance(video, gap);
            evaluation.EducationalValueScore = 0.5;
            evaluation.EvaluationReasoning = $"Evaluation error: {ex.Message}";
        }
    }

    /// <summary>
    /// Simple keyword-based relevance calculation (fallback)
    /// </summary>
    private double CalculateSimpleRelevance(YouTubeSearchResult video, KnowledgeGap gap)
    {
        var searchText = $"{video.Title} {video.Description}".ToLowerInvariant();
        var keywords = gap.Topic.ToLowerInvariant().Split(' ');

        var matchCount = keywords.Count(keyword => searchText.Contains(keyword));
        return (double)matchCount / keywords.Length;
    }

    /// <summary>
    /// Calculate overall quality score from component scores
    /// </summary>
    private double CalculateOverallQualityScore(VideoEvaluation evaluation)
    {
        // Weighted average of all scores
        return (evaluation.RelevanceScore * 0.4) +
               (evaluation.EducationalValueScore * 0.4) +
               (evaluation.EngagementScore * 0.2);
    }

    /// <summary>
    /// Determine recommended action based on scores
    /// </summary>
    private string DetermineRecommendedAction(VideoEvaluation evaluation)
    {
        if (evaluation.QualityScore >= 0.7 && evaluation.RelevanceScore >= 0.6)
        {
            return "Accept";
        }
        else if (evaluation.QualityScore >= 0.5)
        {
            return "NeedsReview";
        }
        else
        {
            return "Reject";
        }
    }
}

