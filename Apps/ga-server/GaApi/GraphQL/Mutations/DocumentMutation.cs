namespace GaApi.GraphQL.Mutations;

using HotChocolate;
using HotChocolate.Types;
using MongoDB.Bson;
using MongoDB.Driver;
using Services;
using Services.AutonomousCuration;
using Services.DocumentProcessing;
using Types;

/// <summary>
/// GraphQL mutations for document management
/// </summary>
[ExtendObjectType("Mutation")]
public class DocumentMutation
{
    /// <summary>
    /// Upload and process a new document
    /// </summary>
    public async Task<DocumentUploadPayload> UploadDocument(
        [Service] MongoDbService mongoDb,
        [Service] DocumentIngestionPipeline pipeline,
        [Service] ILogger<DocumentMutation> logger,
        DocumentUploadInput input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Processing document upload: {SourceType} - {SourceUrl}", input.SourceType, input.SourceUrl);

            // Handle different source types
            if (input.SourceType.Equals("youtube", StringComparison.OrdinalIgnoreCase))
            {
                // Extract video ID from URL
                var videoId = ExtractYouTubeVideoId(input.SourceUrl);
                if (string.IsNullOrEmpty(videoId))
                {
                    return new DocumentUploadPayload
                    {
                        Success = false,
                        ErrorMessage = "Invalid YouTube URL"
                    };
                }

                // Process YouTube video
                var result = await pipeline.ProcessYouTubeVideoAsync(
                    videoId,
                    input.SourceUrl,
                    input.Title ?? "Untitled Video",
                    cancellationToken);

                if (!result.Success)
                {
                    return new DocumentUploadPayload
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage
                    };
                }

                // Retrieve the processed document
                var collection = mongoDb.ProcessedDocuments;
                var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(result.DocumentId!));
                var document = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

                return new DocumentUploadPayload
                {
                    Success = true,
                    DocumentId = result.DocumentId,
                    Document = document != null ? MapBsonToProcessedDocument(document) : null
                };
            }
            else if (input.SourceType.Equals("markdown", StringComparison.OrdinalIgnoreCase) ||
                     input.SourceType.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                // Handle markdown/text content
                if (string.IsNullOrWhiteSpace(input.Content))
                {
                    return new DocumentUploadPayload
                    {
                        Success = false,
                        ErrorMessage = "Content is required for markdown/text documents"
                    };
                }

                // Store directly in MongoDB (simplified processing)
                var collection = mongoDb.ProcessedDocuments;
                var document = new BsonDocument
                {
                    ["source_type"] = input.SourceType,
                    ["source_id"] = Guid.NewGuid().ToString(),
                    ["source_url"] = input.SourceUrl,
                    ["title"] = input.Title ?? "Untitled Document",
                    ["content"] = input.Content,
                    ["processed_at"] = DateTime.UtcNow,
                    ["processingStatus"] = "Pending"
                };

                await collection.InsertOneAsync(document, cancellationToken: cancellationToken);

                return new DocumentUploadPayload
                {
                    Success = true,
                    DocumentId = document["_id"].ToString(),
                    Document = MapBsonToProcessedDocument(document)
                };
            }
            else
            {
                return new DocumentUploadPayload
                {
                    Success = false,
                    ErrorMessage = $"Unsupported source type: {input.SourceType}"
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading document");
            return new DocumentUploadPayload
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Delete a processed document
    /// </summary>
    public async Task<DocumentDeletePayload> DeleteDocument(
        [Service] MongoDbService mongoDb,
        [Service] ILogger<DocumentMutation> logger,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = mongoDb.ProcessedDocuments;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(documentId));
            var result = await collection.DeleteOneAsync(filter, cancellationToken);

            if (result.DeletedCount == 0)
            {
                return new DocumentDeletePayload
                {
                    Success = false,
                    ErrorMessage = "Document not found"
                };
            }

            return new DocumentDeletePayload
            {
                Success = true,
                DeletedDocumentId = documentId
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
            return new DocumentDeletePayload
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Start autonomous curation process
    /// </summary>
    public async Task<AutonomousCurationPayload> StartAutonomousCuration(
        [Service] AutonomousCurationOrchestrator orchestrator,
        [Service] ILogger<DocumentMutation> logger,
        int maxVideosPerGap = 3,
        int maxTotalVideos = 10,
        double minQualityScore = 0.7,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting autonomous curation: maxVideosPerGap={MaxVideosPerGap}, maxTotalVideos={MaxTotalVideos}, minQualityScore={MinQualityScore}",
                maxVideosPerGap, maxTotalVideos, minQualityScore);

            var request = new Models.AutonomousCuration.StartAutonomousCurationRequest
            {
                MaxVideosPerGap = maxVideosPerGap,
                MaxTotalVideos = maxTotalVideos,
                MinQualityScore = minQualityScore
            };

            var result = await orchestrator.StartCurationAsync(request, cancellationToken);

            return new AutonomousCurationPayload
            {
                Success = result.Status == "Completed",
                ProcessedVideos = result.VideosEvaluated,
                AcceptedVideos = result.VideosAccepted,
                Decisions = result.Decisions.Select(d => new CurationDecisionType
                {
                    DecisionTime = d.DecisionTime,
                    Action = d.Action,
                    VideoId = d.VideoId,
                    VideoTitle = d.VideoTitle,
                    VideoUrl = d.VideoUrl,
                    QualityScore = d.QualityScore,
                    Reasoning = d.Reasoning,
                    PositiveFactors = d.PositiveFactors,
                    NegativeFactors = d.NegativeFactors,
                    RelatedGap = d.RelatedGap != null ? new KnowledgeGapType
                    {
                        Category = d.RelatedGap.Category,
                        Topic = d.RelatedGap.Topic,
                        Description = d.RelatedGap.Description,
                        Priority = d.RelatedGap.Priority,
                        SuggestedSearchQuery = d.RelatedGap.SuggestedSearchQuery,
                        PriorityReason = d.RelatedGap.PriorityReason,
                        DependentTopics = d.RelatedGap.DependentTopics,
                        EstimatedLearningTimeMinutes = d.RelatedGap.EstimatedLearningTimeMinutes
                    } : null
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in autonomous curation");
            return new AutonomousCurationPayload
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Reprocess a document (regenerate summary and embeddings)
    /// </summary>
    public async Task<DocumentUploadPayload> ReprocessDocument(
        [Service] MongoDbService mongoDb,
        [Service] DocumentIngestionPipeline pipeline,
        [Service] ILogger<DocumentMutation> logger,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = mongoDb.ProcessedDocuments;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(documentId));
            var document = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

            if (document == null)
            {
                return new DocumentUploadPayload
                {
                    Success = false,
                    ErrorMessage = "Document not found"
                };
            }

            var sourceType = document.GetValue("source_type", "").AsString;

            if (sourceType.Equals("youtube", StringComparison.OrdinalIgnoreCase))
            {
                var videoId = document.GetValue("source_id", "").AsString;
                var videoUrl = document.GetValue("source_url", "").AsString;
                var title = document.GetValue("title", "").AsString;

                // Reprocess the video
                var result = await pipeline.ProcessYouTubeVideoAsync(
                    videoId,
                    videoUrl,
                    title,
                    cancellationToken);

                if (!result.Success)
                {
                    return new DocumentUploadPayload
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage
                    };
                }

                // Retrieve updated document
                var updatedDocument = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

                return new DocumentUploadPayload
                {
                    Success = true,
                    DocumentId = documentId,
                    Document = updatedDocument != null ? MapBsonToProcessedDocument(updatedDocument) : null
                };
            }
            else
            {
                return new DocumentUploadPayload
                {
                    Success = false,
                    ErrorMessage = $"Reprocessing not supported for source type: {sourceType}"
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reprocessing document {DocumentId}", documentId);
            return new DocumentUploadPayload
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // Helper methods
    private static string? ExtractYouTubeVideoId(string url)
    {
        // Handle various YouTube URL formats
        // https://www.youtube.com/watch?v=VIDEO_ID
        // https://youtu.be/VIDEO_ID
        // https://www.youtube.com/embed/VIDEO_ID

        if (url.Contains("youtube.com/watch"))
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["v"];
        }
        else if (url.Contains("youtu.be/"))
        {
            var uri = new Uri(url);
            return uri.Segments.LastOrDefault()?.TrimEnd('/');
        }
        else if (url.Contains("youtube.com/embed/"))
        {
            var uri = new Uri(url);
            return uri.Segments.LastOrDefault()?.TrimEnd('/');
        }

        return null;
    }

    private static ProcessedDocumentType MapBsonToProcessedDocument(BsonDocument doc)
    {
        // Same mapping logic as in DocumentQuery
        var extractedKnowledge = doc.Contains("extracted_knowledge") && doc["extracted_knowledge"].IsBsonDocument
            ? doc["extracted_knowledge"].AsBsonDocument
            : null;

        var metadata = doc.Contains("metadata") && doc["metadata"].IsBsonDocument
            ? doc["metadata"].AsBsonDocument
            : null;

        return new ProcessedDocumentType
        {
            Id = doc["_id"].ToString()!,
            SourceType = doc.GetValue("source_type", "unknown").AsString,
            SourceId = doc.GetValue("source_id", "").AsString,
            SourceUrl = doc.GetValue("source_url", "").AsString,
            Title = doc.GetValue("title", "").AsString,
            Summary = doc.GetValue("summary", "").AsString,
            ProcessedAt = doc.Contains("processed_at") ? doc["processed_at"].ToUniversalTime() : DateTime.UtcNow,
            ProcessingStatus = doc.GetValue("processingStatus", "Unknown").AsString,
            ChunkCount = doc.Contains("chunkCount") ? doc["chunkCount"].AsInt32 : 0,
            ExtractedKnowledge = extractedKnowledge != null ? new ExtractedKnowledgeType
            {
                ChordProgressions = extractedKnowledge.Contains("chord_progressions")
                    ? extractedKnowledge["chord_progressions"].AsBsonArray.Select(x => x.AsString).ToList()
                    : new List<string>(),
                Scales = extractedKnowledge.Contains("scales")
                    ? extractedKnowledge["scales"].AsBsonArray.Select(x => x.AsString).ToList()
                    : new List<string>(),
                Techniques = extractedKnowledge.Contains("techniques")
                    ? extractedKnowledge["techniques"].AsBsonArray.Select(x => x.AsString).ToList()
                    : new List<string>(),
                Concepts = extractedKnowledge.Contains("concepts")
                    ? extractedKnowledge["concepts"].AsBsonArray.Select(x => x.AsString).ToList()
                    : new List<string>(),
                KeyInsights = extractedKnowledge.Contains("key_insights")
                    ? extractedKnowledge["key_insights"].AsBsonArray.Select(x => x.AsString).ToList()
                    : new List<string>()
            } : null,
            Metadata = metadata != null ? new DocumentMetadataType
            {
                VideoId = metadata.Contains("videoId") ? metadata["videoId"].AsString : null,
                ChannelName = metadata.Contains("channelName") ? metadata["channelName"].AsString : null,
                ViewCount = metadata.Contains("viewCount") ? metadata["viewCount"].ToInt64() : null,
                Duration = metadata.Contains("durationSeconds") ? TimeSpan.FromSeconds(metadata["durationSeconds"].AsInt32) : null,
                PublishedAt = metadata.Contains("publishedAt") ? metadata["publishedAt"].ToUniversalTime() : null,
                ThumbnailUrl = metadata.Contains("thumbnailUrl") ? metadata["thumbnailUrl"].AsString : null,
                QualityScore = metadata.Contains("qualityScore") ? metadata["qualityScore"].AsDouble : null,
                RelevanceScore = metadata.Contains("relevanceScore") ? metadata["relevanceScore"].AsDouble : null,
                EducationalValueScore = metadata.Contains("educationalValueScore") ? metadata["educationalValueScore"].AsDouble : null,
                EngagementScore = metadata.Contains("engagementScore") ? metadata["engagementScore"].AsDouble : null
            } : null
        };
    }
}

