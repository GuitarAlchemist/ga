namespace GA.Data.MongoDB.Services.DocumentServices.Rag;

using Embeddings;
using Microsoft.Extensions.Logging;
using Models.Rag;
using global::MongoDB.Driver;

/// <summary>
/// Multi-stage RAG service implementing the NotebookLM pattern:
/// Stage 1: Document analysis and summarization (lightweight processing)
/// Stage 2: Deep LLM processing for embeddings and knowledge graph integration
/// </summary>
/// <typeparam name="TDocument">The RAG document type</typeparam>
public abstract class MultiStageRagService<TDocument>(
    ILogger logger,
    MongoDbService mongoDb,
    IEmbeddingService embeddingService)
    where TDocument : RagDocumentBase
{
    protected readonly ILogger Logger = logger;
    protected readonly MongoDbService MongoDb = mongoDb;
    protected readonly IEmbeddingService EmbeddingService = embeddingService;

    /// <summary>
    /// Stage 1: Analyze and summarize documents
    /// This stage performs lightweight processing to extract key information
    /// </summary>
    /// <param name="documents">Raw documents to analyze</param>
    /// <returns>Analyzed documents with summaries</returns>
    protected abstract Task<List<TDocument>> AnalyzeAndSummarizeAsync(List<TDocument> documents);

    /// <summary>
    /// Stage 2: Deep LLM processing
    /// This stage performs expensive operations like embedding generation and knowledge extraction
    /// </summary>
    /// <param name="documents">Analyzed documents from Stage 1</param>
    /// <returns>Fully processed documents with embeddings</returns>
    protected abstract Task<List<TDocument>> DeepProcessAsync(List<TDocument> documents);

    /// <summary>
    /// Get the MongoDB collection for this document type
    /// </summary>
    protected abstract IMongoCollection<TDocument> GetCollection();

    /// <summary>
    /// Execute the full multi-stage RAG pipeline
    /// </summary>
    /// <param name="rawDocuments">Raw documents to process</param>
    /// <param name="skipStage1">Skip Stage 1 if documents are already analyzed</param>
    /// <param name="skipStage2">Skip Stage 2 if documents don't need deep processing</param>
    /// <returns>True if successful</returns>
    public async Task<bool> ProcessAsync(
        List<TDocument> rawDocuments,
        bool skipStage1 = false,
        bool skipStage2 = false)
    {
        try
        {
            Logger.LogInformation("Starting multi-stage RAG processing for {Count} documents", rawDocuments.Count);

            // Stage 1: Analysis and Summarization
            var analyzedDocuments = skipStage1
                ? rawDocuments
                : await AnalyzeAndSummarizeAsync(rawDocuments);

            Logger.LogInformation("Stage 1 complete: Analyzed {Count} documents", analyzedDocuments.Count);

            // Stage 2: Deep LLM Processing
            var processedDocuments = skipStage2
                ? analyzedDocuments
                : await DeepProcessAsync(analyzedDocuments);

            Logger.LogInformation("Stage 2 complete: Processed {Count} documents", processedDocuments.Count);

            // Store in MongoDB
            var collection = GetCollection();
            await collection.DeleteManyAsync(Builders<TDocument>.Filter.Empty);
            await collection.InsertManyAsync(processedDocuments);

            Logger.LogInformation("Successfully stored {Count} RAG documents", processedDocuments.Count);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in multi-stage RAG processing");
            return false;
        }
    }

    /// <summary>
    /// Process a single document through the pipeline
    /// </summary>
    public async Task<TDocument?> ProcessSingleAsync(TDocument document)
    {
        try
        {
            // Stage 1: Analysis
            var analyzed = await AnalyzeAndSummarizeAsync([document]);
            if (analyzed.Count == 0) return null;

            // Stage 2: Deep processing
            var processed = await DeepProcessAsync(analyzed);
            if (processed.Count == 0) return null;

            // Store in MongoDB
            var collection = GetCollection();
            await collection.ReplaceOneAsync(
                Builders<TDocument>.Filter.Eq(d => d.Id, processed[0].Id),
                processed[0],
                new ReplaceOptions { IsUpsert = true });

            return processed[0];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing single document");
            return null;
        }
    }

    /// <summary>
    /// Batch process documents with configurable batch size
    /// </summary>
    public async Task<bool> ProcessBatchAsync(
        List<TDocument> documents,
        int batchSize = 100,
        bool skipStage1 = false,
        bool skipStage2 = false)
    {
        try
        {
            Logger.LogInformation("Starting batch processing for {Count} documents with batch size {BatchSize}",
                documents.Count, batchSize);

            var batches = documents
                .Select((doc, index) => new { doc, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.doc).ToList())
                .ToList();

            var successCount = 0;
            var failureCount = 0;

            foreach (var batch in batches)
            {
                var success = await ProcessAsync(batch, skipStage1, skipStage2);
                if (success)
                    successCount += batch.Count;
                else
                    failureCount += batch.Count;
            }

            Logger.LogInformation("Batch processing complete: {Success} succeeded, {Failure} failed",
                successCount, failureCount);

            return failureCount == 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in batch processing");
            return false;
        }
    }

    /// <summary>
    /// Generate embeddings for a batch of documents
    /// </summary>
    protected async Task GenerateEmbeddingsAsync(List<TDocument> documents)
    {
        foreach (var doc in documents)
        {
            doc.GenerateSearchText();
            doc.Embedding = [.. (await EmbeddingService.GenerateEmbeddingAsync(doc.SearchText))];
        }
    }

    /// <summary>
    /// Get document count from MongoDB
    /// </summary>
    public async Task<long> GetCountAsync()
    {
        var collection = GetCollection();
        return await collection.CountDocumentsAsync(Builders<TDocument>.Filter.Empty);
    }

    /// <summary>
    /// Search documents by similarity using vector search
    /// </summary>
    public async Task<List<TDocument>> SearchBySimilarityAsync(string query, int limit = 10)
    {
        try
        {
            // Generate embedding for query
            var queryEmbedding = await EmbeddingService.GenerateEmbeddingAsync(query);

            // Perform vector search (simplified - in production use MongoDB Atlas Vector Search)
            var collection = GetCollection();
            var allDocuments = await collection.Find(Builders<TDocument>.Filter.Empty).ToListAsync();

            // Calculate cosine similarity and sort
            var results = allDocuments
                .Select(doc => new
                {
                    Document = doc,
                    Similarity = CosineSimilarity([.. queryEmbedding], doc.Embedding)
                })
                .OrderByDescending(x => x.Similarity)
                .Take(limit)
                .Select(x => x.Document)
                .ToList();

            return results;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in similarity search");
            return [];
        }
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        var dotProduct = a.Zip(b, (x, y) => x * y).Sum();
        var magnitudeA = Math.Sqrt(a.Sum(x => x * x));
        var magnitudeB = Math.Sqrt(b.Sum(x => x * x));

        return (float)(dotProduct / (magnitudeA * magnitudeB));
    }
}

