namespace GaApi.Services.DocumentProcessing;

using GA.Business.Core.AI.Services.Embeddings;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

/// <summary>
/// Pipeline for ingesting and processing documents (YouTube transcripts, PDFs, markdown, web pages)
/// Extracts structured knowledge and generates summaries using Ollama
/// </summary>
public class DocumentIngestionPipeline
{
    private readonly ILogger<DocumentIngestionPipeline> _logger;
    private readonly MongoDbService _mongoDb;
    private readonly IOllamaChatService _ollamaChat;
    private readonly IEmbeddingService _embeddingService;
    private readonly YouTubeTranscriptExtractor _transcriptExtractor;

    public DocumentIngestionPipeline(
        ILogger<DocumentIngestionPipeline> logger,
        MongoDbService mongoDb,
        IOllamaChatService ollamaChat,
        IEmbeddingService embeddingService,
        YouTubeTranscriptExtractor transcriptExtractor)
    {
        _logger = logger;
        _mongoDb = mongoDb;
        _ollamaChat = ollamaChat;
        _embeddingService = embeddingService;
        _transcriptExtractor = transcriptExtractor;
    }

    /// <summary>
    /// Process a YouTube video through the complete ingestion pipeline
    /// </summary>
    public async Task<DocumentProcessingResult> ProcessYouTubeVideoAsync(
        string videoId,
        string videoUrl,
        string title,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting document ingestion for YouTube video: {VideoId}", videoId);

        try
        {
            // Step 1: Extract transcript
            _logger.LogInformation("Extracting transcript for video: {VideoId}", videoId);
            var transcript = await _transcriptExtractor.ExtractTranscriptAsync(videoUrl, cancellationToken);

            if (string.IsNullOrWhiteSpace(transcript))
            {
                _logger.LogWarning("No transcript available for video: {VideoId}", videoId);
                return new DocumentProcessingResult
                {
                    Success = false,
                    ErrorMessage = "No transcript available"
                };
            }

            // Step 2: Chunk the transcript for processing
            var chunks = ChunkText(transcript, maxChunkSize: 2000, overlap: 200);
            _logger.LogInformation("Split transcript into {ChunkCount} chunks", chunks.Count);

            // Step 3: Extract structured knowledge from each chunk
            var extractedKnowledge = new List<ExtractedKnowledge>();
            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                _logger.LogInformation("Processing chunk {Index}/{Total}", index + 1, chunks.Count);
                var knowledge = await ExtractKnowledgeAsync(chunk, cancellationToken);
                extractedKnowledge.Add(knowledge);
            }

            // Step 4: Generate overall summary
            _logger.LogInformation("Generating summary for video: {VideoId}", videoId);
            var summary = await GenerateSummaryAsync(transcript, extractedKnowledge, cancellationToken);

            // Step 5: Generate embeddings for semantic search
            _logger.LogInformation("Generating embeddings for video: {VideoId}", videoId);
            var embeddingArray = await _embeddingService.GenerateEmbeddingAsync(summary);
            var embedding = embeddingArray.ToList();

            // Step 6: Store in MongoDB
            _logger.LogInformation("Storing processed document in MongoDB: {VideoId}", videoId);
            var documentId = await StoreProcessedDocumentAsync(
                videoId,
                videoUrl,
                title,
                transcript,
                summary,
                extractedKnowledge,
                embedding,
                cancellationToken);

            _logger.LogInformation("Successfully processed video: {VideoId}, DocumentId: {DocumentId}",
                videoId, documentId);

            return new DocumentProcessingResult
            {
                Success = true,
                DocumentId = documentId,
                Summary = summary,
                ExtractedKnowledge = extractedKnowledge,
                ChunkCount = chunks.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing YouTube video: {VideoId}", videoId);
            return new DocumentProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Extract structured knowledge from a text chunk using Ollama
    /// </summary>
    private async Task<ExtractedKnowledge> ExtractKnowledgeAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze the following music theory text and extract structured knowledge.
Focus on:
1. Chord progressions (e.g., ii-V-I, I-IV-V)
2. Scales and modes (e.g., Dorian, Mixolydian, harmonic minor)
3. Guitar techniques (e.g., fingerpicking, sweep picking, tapping)
4. Music theory concepts (e.g., voice leading, chord substitution, modal interchange)

Return your analysis in this JSON format:
{{
  ""chordProgressions"": [""progression1"", ""progression2""],
  ""scales"": [""scale1"", ""scale2""],
  ""techniques"": [""technique1"", ""technique2""],
  ""concepts"": [""concept1"", ""concept2""],
  ""keyInsights"": [""insight1"", ""insight2""]
}}

Text to analyze:
{text}

Return ONLY the JSON, no additional text.";

        try
        {
            var response = await _ollamaChat.ChatAsync(prompt, cancellationToken: cancellationToken);

            // Parse JSON response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var knowledge = System.Text.Json.JsonSerializer.Deserialize<ExtractedKnowledge>(json);
                return knowledge ?? new ExtractedKnowledge();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract structured knowledge, using empty result");
        }

        return new ExtractedKnowledge();
    }

    /// <summary>
    /// Generate a summary of the entire document
    /// </summary>
    private async Task<string> GenerateSummaryAsync(
        string fullText,
        List<ExtractedKnowledge> extractedKnowledge,
        CancellationToken cancellationToken)
    {
        // Combine all extracted knowledge
        var allProgressions = extractedKnowledge.SelectMany(k => k.ChordProgressions ?? []).Distinct().ToList();
        var allScales = extractedKnowledge.SelectMany(k => k.Scales ?? []).Distinct().ToList();
        var allTechniques = extractedKnowledge.SelectMany(k => k.Techniques ?? []).Distinct().ToList();
        var allConcepts = extractedKnowledge.SelectMany(k => k.Concepts ?? []).Distinct().ToList();

        var prompt = $@"Create a concise summary (2-3 paragraphs) of this music theory content.

Extracted Knowledge:
- Chord Progressions: {string.Join(", ", allProgressions)}
- Scales: {string.Join(", ", allScales)}
- Techniques: {string.Join(", ", allTechniques)}
- Concepts: {string.Join(", ", allConcepts)}

Full Text (first 1000 chars):
{fullText.Substring(0, Math.Min(1000, fullText.Length))}...

Provide a clear, educational summary suitable for a music theory knowledge base.";

        var summary = await _ollamaChat.ChatAsync(prompt, cancellationToken: cancellationToken);
        return summary;
    }

    /// <summary>
    /// Store the processed document in MongoDB
    /// </summary>
    private async Task<string> StoreProcessedDocumentAsync(
        string videoId,
        string videoUrl,
        string title,
        string transcript,
        string summary,
        List<ExtractedKnowledge> extractedKnowledge,
        List<float> embedding,
        CancellationToken cancellationToken)
    {
        var collection = _mongoDb.Database.GetCollection<BsonDocument>("processed_documents");

        var document = new BsonDocument
        {
            ["source_type"] = "youtube",
            ["source_id"] = videoId,
            ["source_url"] = videoUrl,
            ["title"] = title,
            ["transcript"] = transcript,
            ["summary"] = summary,
            ["extracted_knowledge"] = new BsonDocument
            {
                ["chord_progressions"] = new BsonArray(extractedKnowledge.SelectMany(k => k.ChordProgressions ?? []).Distinct()),
                ["scales"] = new BsonArray(extractedKnowledge.SelectMany(k => k.Scales ?? []).Distinct()),
                ["techniques"] = new BsonArray(extractedKnowledge.SelectMany(k => k.Techniques ?? []).Distinct()),
                ["concepts"] = new BsonArray(extractedKnowledge.SelectMany(k => k.Concepts ?? []).Distinct()),
                ["key_insights"] = new BsonArray(extractedKnowledge.SelectMany(k => k.KeyInsights ?? []).Distinct())
            },
            ["embedding"] = new BsonArray(embedding),
            ["processed_at"] = DateTime.UtcNow,
            ["status"] = "completed"
        };

        await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        return document["_id"].ToString();
    }

    /// <summary>
    /// Chunk text into smaller pieces for processing
    /// </summary>
    private List<string> ChunkText(string text, int maxChunkSize, int overlap)
    {
        var chunks = new List<string>();
        var sentences = text.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new List<string>();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            var sentenceLength = sentence.Length + 2; // +2 for punctuation and space

            if (currentLength + sentenceLength > maxChunkSize && currentChunk.Count > 0)
            {
                // Save current chunk
                chunks.Add(string.Join(". ", currentChunk) + ".");

                // Start new chunk with overlap
                var overlapSentences = currentChunk.TakeLast(Math.Min(3, currentChunk.Count)).ToList();
                currentChunk = overlapSentences;
                currentLength = overlapSentences.Sum(s => s.Length + 2);
            }

            currentChunk.Add(sentence);
            currentLength += sentenceLength;
        }

        // Add final chunk
        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(". ", currentChunk) + ".");
        }

        return chunks;
    }
}

/// <summary>
/// Result of document processing
/// </summary>
public class DocumentProcessingResult
{
    public bool Success { get; set; }
    public string? DocumentId { get; set; }
    public string? Summary { get; set; }
    public List<ExtractedKnowledge>? ExtractedKnowledge { get; set; }
    public int ChunkCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Structured knowledge extracted from text
/// </summary>
public class ExtractedKnowledge
{
    public List<string>? ChordProgressions { get; set; }
    public List<string>? Scales { get; set; }
    public List<string>? Techniques { get; set; }
    public List<string>? Concepts { get; set; }
    public List<string>? KeyInsights { get; set; }
}

