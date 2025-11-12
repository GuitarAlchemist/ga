namespace GaApi.GraphQL.Queries;

using HotChocolate;
using HotChocolate.Types;
using MongoDB.Bson;
using MongoDB.Driver;
using Services;
using Services.AutonomousCuration;
using Types;

/// <summary>
/// GraphQL queries for document management
/// </summary>
[ExtendObjectType("Query")]
public class DocumentQuery
{
    /// <summary>
    /// Get a processed document by ID
    /// </summary>
    public async Task<ProcessedDocumentType?> GetDocument(
        [Service] MongoDbService mongoDb,
        string id)
    {
        var collection = mongoDb.ProcessedDocuments;
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id));
        var document = await collection.Find(filter).FirstOrDefaultAsync();

        return document != null ? MapBsonToProcessedDocument(document) : null;
    }

    /// <summary>
    /// Search for processed documents
    /// </summary>
    public async Task<DocumentSearchResultType> SearchDocuments(
        [Service] MongoDbService mongoDb,
        DocumentSearchInput input)
    {
        var collection = mongoDb.ProcessedDocuments;
        var filterBuilder = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();

        // Text search filter
        if (!string.IsNullOrWhiteSpace(input.Query))
        {
            var textFilter = filterBuilder.Or(
                filterBuilder.Regex("title", new BsonRegularExpression(input.Query, "i")),
                filterBuilder.Regex("summary", new BsonRegularExpression(input.Query, "i")),
                filterBuilder.Regex("extracted_knowledge.concepts", new BsonRegularExpression(input.Query, "i"))
            );
            filters.Add(textFilter);
        }

        // Source type filter
        if (input.SourceTypes?.Any() == true)
        {
            filters.Add(filterBuilder.In("source_type", input.SourceTypes));
        }

        // Date range filter
        if (input.FromDate.HasValue)
        {
            filters.Add(filterBuilder.Gte("processed_at", input.FromDate.Value));
        }

        if (input.ToDate.HasValue)
        {
            filters.Add(filterBuilder.Lte("processed_at", input.ToDate.Value));
        }

        // Quality score filter
        if (input.MinQualityScore.HasValue)
        {
            filters.Add(filterBuilder.Gte("metadata.qualityScore", input.MinQualityScore.Value));
        }

        // Combine filters
        var combinedFilter = filters.Any()
            ? filterBuilder.And(filters)
            : filterBuilder.Empty;

        // Get total count
        var totalCount = (int)await collection.CountDocumentsAsync(combinedFilter);

        // Get paginated results
        var documents = await collection
            .Find(combinedFilter)
            .Sort(Builders<BsonDocument>.Sort.Descending("processed_at"))
            .Skip(input.Skip)
            .Limit(input.Take)
            .ToListAsync();

        var mappedDocuments = documents.Select(MapBsonToProcessedDocument).ToList();

        return new DocumentSearchResultType
        {
            Documents = mappedDocuments,
            TotalCount = totalCount,
            Skip = input.Skip,
            Take = input.Take,
            HasMore = input.Skip + input.Take < totalCount
        };
    }

    /// <summary>
    /// Get all processed documents (paginated)
    /// </summary>
    public async Task<DocumentSearchResultType> GetAllDocuments(
        [Service] MongoDbService mongoDb,
        int skip = 0,
        int take = 20)
    {
        return await SearchDocuments(mongoDb, new DocumentSearchInput
        {
            Skip = skip,
            Take = take
        });
    }

    /// <summary>
    /// Get document processing status
    /// </summary>
    public async Task<DocumentProcessingStatusType?> GetDocumentStatus(
        [Service] MongoDbService mongoDb,
        string documentId)
    {
        var collection = mongoDb.ProcessedDocuments;
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(documentId));
        var document = await collection.Find(filter).FirstOrDefaultAsync();

        if (document == null) return null;

        return new DocumentProcessingStatusType
        {
            DocumentId = documentId,
            Status = document.GetValue("processingStatus", "Unknown").AsString,
            Progress = document.Contains("progress") ? document["progress"].AsInt32 : 100,
            CurrentStep = document.Contains("currentStep") ? document["currentStep"].AsString : null,
            ErrorMessage = document.Contains("errorMessage") ? document["errorMessage"].AsString : null,
            CompletedAt = document.Contains("updatedAt") ? document["updatedAt"].ToUniversalTime() : null
        };
    }

    /// <summary>
    /// Get knowledge gap analysis
    /// </summary>
    public async Task<KnowledgeGapAnalysisType> GetKnowledgeGaps(
        [Service] KnowledgeGapAnalyzer analyzer,
        CancellationToken cancellationToken = default)
    {
        var analysis = await analyzer.AnalyzeGapsAsync(cancellationToken);

        return new KnowledgeGapAnalysisType
        {
            AnalysisDate = analysis.AnalysisDate,
            TotalGaps = analysis.TotalGaps,
            Gaps = analysis.Gaps.Select(g => new KnowledgeGapType
            {
                Category = g.Category,
                Topic = g.Topic,
                Description = g.Description,
                Priority = g.Priority,
                SuggestedSearchQuery = g.SuggestedSearchQuery,
                PriorityReason = g.PriorityReason,
                DependentTopics = g.DependentTopics,
                EstimatedLearningTimeMinutes = g.EstimatedLearningTimeMinutes
            }).ToList()
        };
    }

    /// <summary>
    /// Get recent curation decisions
    /// </summary>
    public async Task<List<CurationDecisionType>> GetRecentCurationDecisions(
        [Service] MongoDbService mongoDb,
        int limit = 20)
    {
        var collection = mongoDb.Database.GetCollection<BsonDocument>("curation_decisions");
        var documents = await collection
            .Find(Builders<BsonDocument>.Filter.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("decisionTime"))
            .Limit(limit)
            .ToListAsync();

        return documents.Select(MapBsonToCurationDecision).ToList();
    }

    /// <summary>
    /// Get documents by knowledge gap category
    /// </summary>
    public async Task<List<ProcessedDocumentType>> GetDocumentsByCategory(
        [Service] MongoDbService mongoDb,
        string category,
        int limit = 20)
    {
        var collection = mongoDb.ProcessedDocuments;
        var filter = Builders<BsonDocument>.Filter.AnyEq("extracted_knowledge.concepts", category);

        var documents = await collection
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("metadata.qualityScore"))
            .Limit(limit)
            .ToListAsync();

        return documents.Select(MapBsonToProcessedDocument).ToList();
    }

    // Helper methods for mapping BSON to GraphQL types
    private static ProcessedDocumentType MapBsonToProcessedDocument(BsonDocument doc)
    {
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

    private static CurationDecisionType MapBsonToCurationDecision(BsonDocument doc)
    {
        var relatedGap = doc.Contains("relatedGap") && doc["relatedGap"].IsBsonDocument
            ? doc["relatedGap"].AsBsonDocument
            : null;

        return new CurationDecisionType
        {
            DecisionTime = doc.Contains("decisionTime") ? doc["decisionTime"].ToUniversalTime() : DateTime.UtcNow,
            Action = doc.GetValue("action", "").AsString,
            VideoId = doc.GetValue("videoId", "").AsString,
            VideoTitle = doc.GetValue("videoTitle", "").AsString,
            VideoUrl = doc.GetValue("videoUrl", "").AsString,
            QualityScore = doc.Contains("qualityScore") ? doc["qualityScore"].AsDouble : 0.0,
            Reasoning = doc.GetValue("reasoning", "").AsString,
            PositiveFactors = doc.Contains("positiveFactors")
                ? doc["positiveFactors"].AsBsonArray.Select(x => x.AsString).ToList()
                : new List<string>(),
            NegativeFactors = doc.Contains("negativeFactors")
                ? doc["negativeFactors"].AsBsonArray.Select(x => x.AsString).ToList()
                : new List<string>(),
            RelatedGap = relatedGap != null ? new KnowledgeGapType
            {
                Category = relatedGap.GetValue("category", "").AsString,
                Topic = relatedGap.GetValue("topic", "").AsString,
                Description = relatedGap.GetValue("description", "").AsString,
                Priority = relatedGap.GetValue("priority", "").AsString,
                SuggestedSearchQuery = relatedGap.GetValue("suggestedSearchQuery", "").AsString,
                PriorityReason = relatedGap.GetValue("priorityReason", "").AsString,
                DependentTopics = relatedGap.Contains("dependentTopics")
                    ? relatedGap["dependentTopics"].AsBsonArray.Select(x => x.AsString).ToList()
                    : new List<string>(),
                EstimatedLearningTimeMinutes = relatedGap.Contains("estimatedLearningTimeMinutes")
                    ? relatedGap["estimatedLearningTimeMinutes"].AsInt32
                    : 0
            } : null
        };
    }
}

