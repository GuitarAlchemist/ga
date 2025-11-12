namespace GA.DocumentProcessing.Service.Services;

using Models;

/// <summary>
/// Orchestrates autonomous YouTube video curation based on knowledge gaps
/// Uses Graphiti knowledge graph to track learning progress and guide curation
/// </summary>
public class AutonomousCurationOrchestrator
{
    private readonly ILogger<AutonomousCurationOrchestrator> _logger;
    private readonly KnowledgeGapAnalyzer _gapAnalyzer;
    private readonly YouTubeSearchService _youtubeSearch;
    private readonly VideoQualityEvaluator _qualityEvaluator;
    private readonly RetroactionLoopOrchestrator _retroactionLoop;
    private readonly MongoDbService _mongoDbService;

    public AutonomousCurationOrchestrator(
        ILogger<AutonomousCurationOrchestrator> logger,
        KnowledgeGapAnalyzer gapAnalyzer,
        YouTubeSearchService youtubeSearch,
        VideoQualityEvaluator qualityEvaluator,
        RetroactionLoopOrchestrator retroactionLoop,
        MongoDbService mongoDbService)
    {
        _logger = logger;
        _gapAnalyzer = gapAnalyzer;
        _youtubeSearch = youtubeSearch;
        _qualityEvaluator = qualityEvaluator;
        _retroactionLoop = retroactionLoop;
        _mongoDbService = mongoDbService;
    }

    /// <summary>
    /// Start autonomous curation process
    /// </summary>
    public async Task<AutonomousCurationResult> StartCurationAsync(
        StartAutonomousCurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new AutonomousCurationResult
        {
            StartTime = DateTime.UtcNow,
            Status = "Running"
        };

        try
        {
            _logger.LogInformation("Starting autonomous curation with max {MaxVideos} videos", request.MaxTotalVideos);

            // Step 1: Analyze knowledge gaps
            var gapAnalysis = await _gapAnalyzer.AnalyzeGapsAsync(cancellationToken);
            result.GapsAnalyzed = gapAnalysis.Gaps.Count;

            // Step 2: Filter gaps based on request criteria
            var targetGaps = FilterGaps(gapAnalysis.Gaps, request);
            _logger.LogInformation("Targeting {Count} gaps for curation", targetGaps.Count);

            // Step 3: For each gap, search and evaluate videos
            var videosProcessed = 0;
            foreach (var gap in targetGaps)
            {
                if (videosProcessed >= request.MaxTotalVideos)
                {
                    _logger.LogInformation("Reached max video limit ({Max}), stopping curation", request.MaxTotalVideos);
                    break;
                }

                await ProcessGapAsync(gap, request, result, cancellationToken);
                videosProcessed = result.VideosAccepted + result.VideosRejected;
            }

            result.Status = "Completed";
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Autonomous curation complete: {Accepted} accepted, {Rejected} rejected out of {Total} evaluated",
                result.VideosAccepted,
                result.VideosRejected,
                result.VideosEvaluated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during autonomous curation");
            result.Status = "Failed";
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Process a single knowledge gap: search, evaluate, and decide
    /// </summary>
    private async Task ProcessGapAsync(
        KnowledgeGap gap,
        StartAutonomousCurationRequest request,
        AutonomousCurationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing gap: {Category} - {Topic}", gap.Category, gap.Topic);

            // Step 1: Search YouTube for videos
            var searchResults = await _youtubeSearch.SearchAsync(
                gap.SuggestedSearchQuery,
                request.MaxVideosPerGap,
                cancellationToken);

            result.VideosFound += searchResults.Count;

            // Step 2: Evaluate each video
            foreach (var video in searchResults)
            {
                var evaluation = await _qualityEvaluator.EvaluateVideoAsync(video, gap, cancellationToken);
                result.VideosEvaluated++;

                // Step 3: Make curation decision
                var decision = MakeDecision(video, gap, evaluation, request.MinQualityScore);
                result.Decisions.Add(decision);

                // Step 4: If accepted, start retroaction loop
                if (decision.Action == "Accept")
                {
                    result.VideosAccepted++;
                    await ProcessAcceptedVideoAsync(video, gap, cancellationToken);
                }
                else
                {
                    result.VideosRejected++;
                }

                _logger.LogInformation(
                    "Decision for {VideoId}: {Action} (Quality: {Score:F2})",
                    video.VideoId,
                    decision.Action,
                    evaluation.QualityScore);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing gap: {Topic}", gap.Topic);
        }
    }

    /// <summary>
    /// Make a curation decision based on evaluation
    /// </summary>
    private CurationDecision MakeDecision(
        YouTubeSearchResult video,
        KnowledgeGap gap,
        VideoEvaluation evaluation,
        double minQualityScore)
    {
        var decision = new CurationDecision
        {
            DecisionTime = DateTime.UtcNow,
            VideoId = video.VideoId,
            VideoTitle = video.Title,
            VideoUrl = video.Url,
            RelatedGap = gap,
            QualityScore = evaluation.QualityScore
        };

        // Decision logic
        if (evaluation.QualityScore >= minQualityScore)
        {
            decision.Action = "Accept";
            decision.Reasoning = $"High quality video (score: {evaluation.QualityScore:F2}) that addresses {gap.Topic}";
            decision.PositiveFactors.Add($"Quality score: {evaluation.QualityScore:F2}");
            decision.PositiveFactors.Add($"Relevance: {evaluation.RelevanceScore:F2}");
            decision.PositiveFactors.Add($"Educational value: {evaluation.EducationalValueScore:F2}");
        }
        else if (evaluation.QualityScore >= minQualityScore * 0.8)
        {
            decision.Action = "NeedsReview";
            decision.Reasoning = $"Borderline quality (score: {evaluation.QualityScore:F2}), needs human review";
            decision.NegativeFactors.Add($"Quality score below threshold: {evaluation.QualityScore:F2} < {minQualityScore:F2}");
        }
        else
        {
            decision.Action = "Reject";
            decision.Reasoning = $"Low quality video (score: {evaluation.QualityScore:F2})";
            decision.NegativeFactors.Add($"Quality score too low: {evaluation.QualityScore:F2}");
            decision.NegativeFactors.Add(evaluation.EvaluationReasoning);
        }

        return decision;
    }

    /// <summary>
    /// Process an accepted video through the retroaction loop
    /// </summary>
    private async Task ProcessAcceptedVideoAsync(
        YouTubeSearchResult video,
        KnowledgeGap gap,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing accepted video: {VideoId} for gap: {Topic}", video.VideoId, gap.Topic);

            // Start retroaction loop for this video
            var retroactionRequest = new StartYouTubeRetroactionLoopRequest
            {
                YouTubeUrl = video.Url,
                Focus = $"{gap.Category}: {gap.Topic}",
                MaxIterations = 3,
                ConvergenceThreshold = 0.85
            };

            await _retroactionLoop.StartYouTubeRetroactionLoopAsync(retroactionRequest, cancellationToken);

            // TODO: Update Graphiti knowledge graph with new knowledge
            // await UpdateGraphitiAsync(video, gap, extractedKnowledge, cancellationToken);

            _logger.LogInformation("Successfully processed video {VideoId}", video.VideoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing accepted video: {VideoId}", video.VideoId);
        }
    }

    /// <summary>
    /// Filter gaps based on request criteria
    /// </summary>
    private List<KnowledgeGap> FilterGaps(List<KnowledgeGap> gaps, StartAutonomousCurationRequest request)
    {
        var filtered = gaps.AsEnumerable();

        // Filter by category
        if (request.FocusCategories.Any())
        {
            filtered = filtered.Where(g => request.FocusCategories.Contains(g.Category));
        }

        // Filter by priority
        if (request.FocusPriorities.Any())
        {
            filtered = filtered.Where(g => request.FocusPriorities.Contains(g.Priority));
        }

        // Sort by priority
        var priorityOrder = new Dictionary<string, int>
        {
            ["Critical"] = 4,
            ["High"] = 3,
            ["Medium"] = 2,
            ["Low"] = 1
        };

        return filtered
            .OrderByDescending(g => priorityOrder.GetValueOrDefault(g.Priority, 0))
            .ToList();
    }

    /// <summary>
    /// Update Graphiti knowledge graph with new knowledge from video
    /// </summary>
    private async Task UpdateGraphitiAsync(
        YouTubeSearchResult video,
        KnowledgeGap gap,
        ExtractedKnowledge knowledge,
        CancellationToken cancellationToken)
    {
        // TODO: Implement Graphiti integration
        // This will:
        // 1. Add video as a learning episode
        // 2. Add extracted knowledge (chords, scales, techniques) as nodes
        // 3. Create relationships between concepts
        // 4. Update user learning progress
        // 5. Mark the knowledge gap as filled

        _logger.LogInformation("Graphiti integration not yet implemented");
        await Task.CompletedTask;
    }
}

