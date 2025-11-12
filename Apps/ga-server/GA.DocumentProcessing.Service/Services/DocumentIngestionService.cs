namespace GA.DocumentProcessing.Service.Services;

using GA.DocumentProcessing.Service.Models;
using GA.Data.SemanticKernel.Embeddings;
using MongoDB.Driver;
using System.Diagnostics;

/// <summary>
/// Main service orchestrating the NotebookLM-style document processing pipeline
/// </summary>
public class DocumentIngestionService
{
    private readonly MongoDbService _mongoDb;
    private readonly PdfProcessorService _pdfProcessor;
    private readonly MarkdownProcessorService _markdownProcessor;
    private readonly OllamaSummarizationService _ollamaService;
    private readonly KnowledgeExtractionService _knowledgeExtractor;
    private readonly IBatchEmbeddingService _embeddingService;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        MongoDbService mongoDb,
        PdfProcessorService pdfProcessor,
        MarkdownProcessorService markdownProcessor,
        OllamaSummarizationService ollamaService,
        KnowledgeExtractionService knowledgeExtractor,
        IBatchEmbeddingService embeddingService,
        ILogger<DocumentIngestionService> logger)
    {
        _mongoDb = mongoDb;
        _pdfProcessor = pdfProcessor;
        _markdownProcessor = markdownProcessor;
        _ollamaService = ollamaService;
        _knowledgeExtractor = knowledgeExtractor;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Process text directly (for YouTube transcripts, etc.)
    /// </summary>
    public async Task<ProcessedDocument> ProcessTextAsync(
        string text,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        var document = new ProcessedDocument
        {
            SourceName = sourceName,
            DocumentType = "text",
            RawText = text,
            Status = ProcessingStatus.Summarizing,
            Metadata = new ProcessingMetadata
            {
                CharacterCount = text.Length,
                WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
            }
        };

        await _mongoDb.Documents.InsertOneAsync(document, cancellationToken: cancellationToken);

        try
        {
            // Generate summary
            document.Summary = await _ollamaService.GenerateSummaryAsync(text, cancellationToken);

            // Extract knowledge
            document.Status = ProcessingStatus.Extracting;
            await UpdateDocumentAsync(document, cancellationToken);

            var knowledge = await _knowledgeExtractor.ExtractKnowledgeAsync(text, document.Summary, cancellationToken);
            document.Knowledge = knowledge;

            // Generate embeddings
            document.Status = ProcessingStatus.Embedding;
            await UpdateDocumentAsync(document, cancellationToken);

            var chunks = SplitIntoChunks(text, 1000, 200);
            var embeddings = await _embeddingService.GenerateBatchEmbeddingsAsync(chunks.ToArray(), cancellationToken);
            document.Embeddings = embeddings.ToList();

            document.Status = ProcessingStatus.Completed;
            await UpdateDocumentAsync(document, cancellationToken);

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing text");
            document.Status = ProcessingStatus.Failed;
            document.ErrorMessage = ex.Message;
            await UpdateDocumentAsync(document, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Process a document through the complete pipeline
    /// </summary>
    public async Task<ProcessedDocument> ProcessDocumentAsync(
        string sourceName,
        string documentType,
        Stream contentStream,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Create initial document record
        var document = new ProcessedDocument
        {
            SourceName = sourceName,
            DocumentType = documentType,
            RawText = string.Empty,
            Status = ProcessingStatus.Pending,
            Tags = tags ?? new List<string>()
        };

        await _mongoDb.Documents.InsertOneAsync(document, cancellationToken: cancellationToken);
        _logger.LogInformation("Created document record {DocumentId} for {SourceName}", document.Id, sourceName);

        try
        {
            // Stage 1: Extract text
            document.Status = ProcessingStatus.Extracting;
            await UpdateDocumentAsync(document, cancellationToken);

            var (text, pageCount) = await ExtractTextAsync(documentType, contentStream, cancellationToken);
            document.RawText = text;
            document.Metadata.CharacterCount = text.Length;
            document.Metadata.WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            document.Metadata.PageCount = pageCount;

            // Stage 2: Generate summary using Ollama (NotebookLM Stage 1)
            document.Status = ProcessingStatus.Summarizing;
            await UpdateDocumentAsync(document, cancellationToken);

            document.Summary = await _ollamaService.GenerateSummaryAsync(text, cancellationToken);

            // Stage 3: Extract structured knowledge (NotebookLM Stage 2)
            document.Status = ProcessingStatus.ExtractingKnowledge;
            await UpdateDocumentAsync(document, cancellationToken);

            document.Knowledge = await _knowledgeExtractor.ExtractKnowledgeAsync(text, document.Summary, cancellationToken);

            // Stage 4: Generate embeddings for semantic search
            document.Status = ProcessingStatus.GeneratingEmbeddings;
            await UpdateDocumentAsync(document, cancellationToken);

            var embeddingText = $"{document.SourceName}\n{document.Summary}\n{string.Join(" ", document.Knowledge.ChordProgressions)}";
            document.Embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText, cancellationToken);

            // Complete
            document.Status = ProcessingStatus.Completed;
            document.ProcessedAt = DateTime.UtcNow;
            stopwatch.Stop();
            document.Metadata.ProcessingDuration = stopwatch.Elapsed;
            document.Metadata.OllamaModel = "llama3.2:latest";

            await UpdateDocumentAsync(document, cancellationToken);

            _logger.LogInformation("Successfully processed document {DocumentId} in {Duration}ms",
                document.Id, stopwatch.ElapsedMilliseconds);

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", document.Id);

            document.Status = ProcessingStatus.Failed;
            document.Metadata.ErrorMessage = ex.Message;
            await UpdateDocumentAsync(document, cancellationToken);

            throw;
        }
    }

    private async Task<(string Text, int PageCount)> ExtractTextAsync(
        string documentType,
        Stream contentStream,
        CancellationToken cancellationToken)
    {
        return documentType.ToLowerInvariant() switch
        {
            "pdf" => await _pdfProcessor.ExtractTextAsync(contentStream),
            "md" or "markdown" => (await _markdownProcessor.ExtractTextFromStreamAsync(contentStream), 1),
            "txt" => (await new StreamReader(contentStream).ReadToEndAsync(cancellationToken), 1),
            _ => throw new ArgumentException($"Unsupported document type: {documentType}")
        };
    }

    private async Task UpdateDocumentAsync(ProcessedDocument document, CancellationToken cancellationToken)
    {
        var filter = Builders<ProcessedDocument>.Filter.Eq(d => d.Id, document.Id);
        await _mongoDb.Documents.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Split text into overlapping chunks for embedding
    /// </summary>
    private List<string> SplitIntoChunks(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i += chunkSize - overlap)
        {
            var chunk = string.Join(" ", words.Skip(i).Take(chunkSize));
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    public async Task<ProcessedDocument?> GetDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ProcessedDocument>.Filter.Eq(d => d.Id, documentId);
        return await _mongoDb.Documents.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Search documents by semantic similarity
    /// </summary>
    public async Task<List<ProcessedDocument>> SearchDocumentsAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        // Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // For now, return all completed documents (vector search would be implemented with MongoDB Atlas Vector Search)
        var filter = Builders<ProcessedDocument>.Filter.Eq(d => d.Status, ProcessingStatus.Completed);
        var documents = await _mongoDb.Documents.Find(filter)
            .Limit(maxResults)
            .ToListAsync(cancellationToken);

        return documents;
    }

    /// <summary>
    /// Get document statistics
    /// </summary>
    public async Task<DocumentStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var allDocs = await _mongoDb.Documents.Find(_ => true).ToListAsync(cancellationToken);

        var stats = new DocumentStatistics
        {
            TotalDocuments = allDocs.Count,
            ProcessedDocuments = allDocs.Count(d => d.Status == ProcessingStatus.Completed),
            PendingDocuments = allDocs.Count(d => d.Status == ProcessingStatus.Pending),
            FailedDocuments = allDocs.Count(d => d.Status == ProcessingStatus.Failed),
            DocumentsByType = allDocs.GroupBy(d => d.DocumentType).ToDictionary(g => g.Key, g => g.Count()),
            DocumentsByTag = allDocs.SelectMany(d => d.Tags).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count())
        };

        return stats;
    }
}

