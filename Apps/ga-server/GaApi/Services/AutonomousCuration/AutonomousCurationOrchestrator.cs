namespace GaApi.Services.AutonomousCuration;

using DocumentProcessing;
using GA.Business.Graphiti.Models;
using GA.Business.Graphiti.Services;
using Models.AutonomousCuration;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Orchestrates the autonomous YouTube video curation process
/// Analyzes knowledge gaps, searches for videos, evaluates quality, and makes decisions
/// </summary>
public class AutonomousCurationOrchestrator
{
    private readonly ILogger<AutonomousCurationOrchestrator> _logger;
    private readonly KnowledgeGapAnalyzer _gapAnalyzer;
    private readonly YouTubeSearchService _youtubeSearch;
    private readonly VideoQualityEvaluator _qualityEvaluator;
    private readonly MongoDbService _mongoDb;
    private readonly IGraphitiService _graphitiService;
    private readonly DocumentIngestionPipeline _documentPipeline;

    public AutonomousCurationOrchestrator(
        ILogger<AutonomousCurationOrchestrator> logger,
        KnowledgeGapAnalyzer gapAnalyzer,
        YouTubeSearchService youtubeSearch,
        VideoQualityEvaluator qualityEvaluator,
        MongoDbService mongoDb,
        IGraphitiService graphitiService,
        DocumentIngestionPipeline documentPipeline)
    {
        _logger = logger;
        _gapAnalyzer = gapAnalyzer;
        _youtubeSearch = youtubeSearch;
        _qualityEvaluator = qualityEvaluator;
        _mongoDb = mongoDb;
        _graphitiService = graphitiService;
        _documentPipeline = documentPipeline;
    }

    /// <summary>
    /// Start autonomous curation process
    /// </summary>
    public async Task<AutonomousCurationResult> StartCurationAsync(
        StartAutonomousCurationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting autonomous curation");

        var result = new AutonomousCurationResult
        {
            StartTime = DateTime.UtcNow,
            Status = "Running"
        };

        try
        {
            // Step 1: Analyze knowledge gaps
            var analysis = await _gapAnalyzer.AnalyzeGapsAsync(cancellationToken);
            var gaps = FilterGaps(analysis.Gaps, request);
            result.GapsAnalyzed = gaps.Count;

            _logger.LogInformation("Analyzing {Count} knowledge gaps", gaps.Count);

            // Step 2: Process each gap
            var videosProcessed = 0;
            foreach (var gap in gaps)
            {
                if (videosProcessed >= request.MaxTotalVideos)
                {
                    _logger.LogInformation("Reached max total videos limit ({Max})", request.MaxTotalVideos);
                    break;
                }

                var gapDecisions = await ProcessGapAsync(gap, request, cancellationToken);
                result.Decisions.AddRange(gapDecisions);

                videosProcessed += gapDecisions.Count(d => d.Action == "Accept");
            }

            // Step 3: Calculate statistics
            result.VideosFound = result.Decisions.Count;
            result.VideosEvaluated = result.Decisions.Count;
            result.VideosAccepted = result.Decisions.Count(d => d.Action == "Accept");
            result.VideosRejected = result.Decisions.Count(d => d.Action == "Reject");

            result.Status = "Completed";
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Autonomous curation complete: {Accepted} accepted, {Rejected} rejected",
                result.VideosAccepted,
                result.VideosRejected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during autonomous curation");
            result.Status = $"Failed: {ex.Message}";
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Process a single knowledge gap
    /// </summary>
    private async Task<List<CurationDecision>> ProcessGapAsync(
        KnowledgeGap gap,
        StartAutonomousCurationRequest request,
        CancellationToken cancellationToken)
    {
        var decisions = new List<CurationDecision>();

        try
        {
            _logger.LogInformation("Processing gap: {Category} - {Topic}", gap.Category, gap.Topic);

            // Search YouTube for videos
            var videos = await _youtubeSearch.SearchAsync(
                gap.SuggestedSearchQuery,
                request.MaxVideosPerGap,
                cancellationToken);

            _logger.LogInformation("Found {Count} videos for gap: {Topic}", videos.Count, gap.Topic);

            // Evaluate each video
            foreach (var video in videos)
            {
                var evaluation = await _qualityEvaluator.EvaluateVideoAsync(video, gap, cancellationToken);

                var decision = MakeDecision(video, gap, evaluation, request.MinQualityScore);
                decisions.Add(decision);

                // If accepted, process through document ingestion pipeline
                if (decision.Action == "Accept")
                {
                    await ProcessAcceptedVideoAsync(video, evaluation, gap, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing gap: {Topic}", gap.Topic);
        }

        return decisions;
    }

    /// <summary>
    /// Make curation decision based on evaluation
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

        if (evaluation.QualityScore >= minQualityScore)
        {
            decision.Action = "Accept";
            decision.Reasoning = $"High quality video (score: {evaluation.QualityScore:F2}) that addresses {gap.Topic}";
            decision.PositiveFactors = evaluation.PositiveFactors;
        }
        else if (evaluation.QualityScore >= minQualityScore * 0.8)
        {
            decision.Action = "NeedsReview";
            decision.Reasoning = $"Borderline quality (score: {evaluation.QualityScore:F2}), manual review recommended";
            decision.PositiveFactors = evaluation.PositiveFactors;
            decision.NegativeFactors = evaluation.NegativeFactors;
        }
        else
        {
            decision.Action = "Reject";
            decision.Reasoning = $"Low quality (score: {evaluation.QualityScore:F2}), does not meet threshold";
            decision.NegativeFactors = evaluation.NegativeFactors;
        }

        return decision;
    }

    /// <summary>
    /// Process accepted video (store metadata, trigger retroaction loop)
    /// </summary>
    private async Task ProcessAcceptedVideoAsync(
        YouTubeSearchResult video,
        VideoEvaluation evaluation,
        KnowledgeGap gap,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing accepted video: {VideoId} - {Title}", video.VideoId, video.Title);

            // Store video metadata in MongoDB
            var document = new BsonDocument
            {
                { "title", video.Title },
                { "content", video.Description },
                { "category", gap.Category },
                { "tags", new BsonArray { gap.Topic, video.ChannelName } },
                { "processingStatus", "Pending" },
                { "createdAt", DateTime.UtcNow },
                { "updatedAt", DateTime.UtcNow },
                { "metadata", new BsonDocument
                    {
                        { "videoId", video.VideoId },
                        { "videoUrl", video.Url },
                        { "channelName", video.ChannelName },
                        { "viewCount", video.ViewCount },
                        { "duration", video.Duration.ToString() },
                        { "publishedAt", video.PublishedAt },
                        { "knowledgeGap", gap.Topic },
                        { "qualityScore", evaluation.QualityScore }
                    }
                }
            };

            await _mongoDb.ProcessedDocuments.InsertOneAsync(document, cancellationToken: cancellationToken);

            _logger.LogInformation("Stored video metadata for {VideoId}", video.VideoId);

            // Process through document ingestion pipeline
            _logger.LogInformation("Starting document ingestion for video: {VideoId}", video.VideoId);
            var processingResult = await _documentPipeline.ProcessYouTubeVideoAsync(
                video.VideoId,
                video.Url,
                video.Title,
                cancellationToken);

            if (processingResult.Success)
            {
                _logger.LogInformation("Successfully ingested document for video: {VideoId}, DocumentId: {DocumentId}",
                    video.VideoId, processingResult.DocumentId);

                // Update status in processed_documents collection
                var filter = Builders<BsonDocument>.Filter.Eq("metadata.videoId", video.VideoId);
                var update = Builders<BsonDocument>.Update
                    .Set("processingStatus", "Completed")
                    .Set("processedDocumentId", processingResult.DocumentId)
                    .Set("summary", processingResult.Summary)
                    .Set("chunkCount", processingResult.ChunkCount)
                    .Set("updatedAt", DateTime.UtcNow);

                await _mongoDb.ProcessedDocuments.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            }
            else
            {
                _logger.LogWarning("Failed to ingest document for video: {VideoId}, Error: {Error}",
                    video.VideoId, processingResult.ErrorMessage);

                // Update status to failed
                var filter = Builders<BsonDocument>.Filter.Eq("metadata.videoId", video.VideoId);
                var update = Builders<BsonDocument>.Update
                    .Set("processingStatus", "Failed")
                    .Set("errorMessage", processingResult.ErrorMessage)
                    .Set("updatedAt", DateTime.UtcNow);

                await _mongoDb.ProcessedDocuments.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            }

            // Update Graphiti knowledge graph
            await UpdateGraphitiAsync(video, evaluation, gap, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing accepted video {VideoId}", video.VideoId);
        }
    }

    /// <summary>
    /// Update Graphiti knowledge graph with new video knowledge
    /// </summary>
    private async Task UpdateGraphitiAsync(
        YouTubeSearchResult video,
        VideoEvaluation evaluation,
        KnowledgeGap gap,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating Graphiti knowledge graph for video {VideoId}", video.VideoId);

            // Create an episode representing the knowledge acquisition from this video
            var episodeRequest = new EpisodeRequest
            {
                UserId = "autonomous-curator", // System user for autonomous curation
                EpisodeType = "knowledge_acquisition",
                Content = new Dictionary<string, object>
                {
                    ["source"] = "youtube",
                    ["video_id"] = video.VideoId,
                    ["video_url"] = video.Url,
                    ["video_title"] = video.Title,
                    ["channel_name"] = video.ChannelName,
                    ["knowledge_gap_category"] = gap.Category,
                    ["knowledge_gap_topic"] = gap.Topic,
                    ["knowledge_gap_priority"] = gap.Priority,
                    ["quality_score"] = evaluation.QualityScore,
                    ["relevance_score"] = evaluation.RelevanceScore,
                    ["educational_value_score"] = evaluation.EducationalValueScore,
                    ["engagement_score"] = evaluation.EngagementScore,
                    ["view_count"] = video.ViewCount,
                    ["duration"] = video.Duration.ToString(),
                    ["published_at"] = video.PublishedAt.ToString("O"),
                    ["description"] = video.Description,

                    // Knowledge entities extracted from the video
                    ["entities"] = new Dictionary<string, object>
                    {
                        ["topic"] = gap.Topic,
                        ["category"] = gap.Category,
                        ["source_type"] = "educational_video",
                        ["learning_context"] = "autonomous_curation"
                    },

                    // Relationships to establish in the knowledge graph
                    ["relationships"] = new List<Dictionary<string, string>>
                    {
                        new()
                        {
                            ["type"] = "fills_knowledge_gap",
                            ["from"] = video.Title,
                            ["to"] = gap.Topic,
                            ["strength"] = evaluation.QualityScore.ToString("F2")
                        },
                        new()
                        {
                            ["type"] = "belongs_to_category",
                            ["from"] = gap.Topic,
                            ["to"] = gap.Category,
                            ["strength"] = "1.0"
                        },
                        new()
                        {
                            ["type"] = "created_by",
                            ["from"] = video.Title,
                            ["to"] = video.ChannelName,
                            ["strength"] = "1.0"
                        }
                    }
                },
                Timestamp = DateTime.UtcNow
            };

            var response = await _graphitiService.AddEpisodeAsync(episodeRequest, cancellationToken);

            if (response.Status == "success")
            {
                _logger.LogInformation(
                    "Successfully updated Graphiti knowledge graph for video {VideoId}",
                    video.VideoId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to update Graphiti knowledge graph for video {VideoId}: {Message}",
                    video.VideoId,
                    response.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating Graphiti knowledge graph for video {VideoId}",
                video.VideoId);
            // Don't throw - Graphiti update failure shouldn't stop the curation process
        }
    }

    /// <summary>
    /// Filter gaps based on request criteria
    /// </summary>
    private List<KnowledgeGap> FilterGaps(List<KnowledgeGap> gaps, StartAutonomousCurationRequest request)
    {
        var filtered = gaps.AsEnumerable();

        if (request.FocusCategories.Any())
        {
            filtered = filtered.Where(g => request.FocusCategories.Contains(g.Category));
        }

        if (request.FocusPriorities.Any())
        {
            filtered = filtered.Where(g => request.FocusPriorities.Contains(g.Priority));
        }

        return filtered.ToList();
    }
}

