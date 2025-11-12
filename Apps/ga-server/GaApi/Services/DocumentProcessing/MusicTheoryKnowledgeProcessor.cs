namespace GaApi.Services.DocumentProcessing;

using GA.Data.MongoDB;
using GA.Data.MongoDB.Services.Embeddings;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

/// <summary>
/// Specialized processor for harmony textbooks and jazz theory books
/// Extracts chord-scale relationships, voice leading rules, harmonic progressions, and theory concepts
/// Enables chatbot queries like "what scales work over altered dominant chords?"
/// </summary>
public class MusicTheoryKnowledgeProcessor
{
    private readonly ILogger<MusicTheoryKnowledgeProcessor> _logger;
    private readonly MongoDbService _mongoDb;
    private readonly IOllamaChatService _ollamaChat;
    private readonly IEmbeddingService _embeddingService;

    public MusicTheoryKnowledgeProcessor(
        ILogger<MusicTheoryKnowledgeProcessor> logger,
        MongoDbService mongoDb,
        IOllamaChatService ollamaChat,
        IEmbeddingService embeddingService)
    {
        _logger = logger;
        _mongoDb = mongoDb;
        _ollamaChat = ollamaChat;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Process a music theory document (PDF, markdown, or text)
    /// </summary>
    public async Task<MusicTheoryProcessingResult> ProcessDocumentAsync(
        string documentId,
        string title,
        string content,
        string sourceType,
        string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing music theory document: {Title}", title);

        try
        {
            // Step 1: Chunk the content for processing
            var chunks = ChunkText(content, maxChunkSize: 2000, overlap: 200);
            _logger.LogInformation("Split document into {ChunkCount} chunks", chunks.Count);

            // Step 2: Extract music theory knowledge from each chunk
            var extractedTheory = new List<MusicTheoryKnowledge>();
            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                _logger.LogInformation("Processing chunk {Index}/{Total}", index + 1, chunks.Count);
                var knowledge = await ExtractMusicTheoryAsync(chunk, cancellationToken);
                extractedTheory.Add(knowledge);
            }

            // Step 3: Generate summary
            _logger.LogInformation("Generating summary for document: {Title}", title);
            var summary = await GenerateSummaryAsync(content, extractedTheory, cancellationToken);

            // Step 4: Generate embeddings for semantic search
            _logger.LogInformation("Generating embeddings for document: {Title}", title);
            var embedding = await _embeddingService.GenerateEmbeddingAsync(summary);

            // Step 5: Store in MongoDB
            _logger.LogInformation("Storing music theory document in MongoDB: {Title}", title);
            var storedDocId = await StoreMusicTheoryDocumentAsync(
                documentId,
                title,
                content,
                summary,
                extractedTheory,
                embedding,
                sourceType,
                sourceUrl,
                cancellationToken);

            return new MusicTheoryProcessingResult
            {
                Success = true,
                DocumentId = storedDocId,
                Summary = summary,
                ExtractedTheory = extractedTheory,
                ChunkCount = chunks.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing music theory document: {Title}", title);
            return new MusicTheoryProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Extract music theory knowledge from a text chunk using Ollama
    /// </summary>
    private async Task<MusicTheoryKnowledge> ExtractMusicTheoryAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze the following music theory text and extract structured knowledge.
Focus on:
1. Chord-scale relationships (e.g., Dorian over minor 7th, Mixolydian over dominant 7th, altered scale over altered dominants)
2. Voice leading rules (e.g., common tone retention, contrary motion, voice crossing)
3. Harmonic progressions (e.g., ii-V-I, circle of fifths, modal interchange)
4. Chord substitutions (e.g., tritone substitution, diminished substitution, secondary dominants)
5. Modal theory (e.g., Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian)
6. Jazz harmony concepts (e.g., upper structure triads, polychords, quartal harmony)
7. Functional harmony (e.g., tonic, subdominant, dominant functions)
8. Theoretical concepts (e.g., enharmonic equivalence, voice leading spaces, neo-Riemannian theory)

Text to analyze:
{text}

Return a JSON object with this structure:
{{
  ""chordScaleRelationships"": [""chord type: applicable scales""],
  ""voiceLeadingRules"": [""rule description""],
  ""harmonicProgressions"": [""progression with analysis""],
  ""chordSubstitutions"": [""substitution type and example""],
  ""modalTheory"": [""mode name and characteristics""],
  ""jazzHarmonyConcepts"": [""concept name and description""],
  ""functionalHarmony"": [""function and chord types""],
  ""theoreticalConcepts"": [""concept name and explanation""],
  ""keyInsights"": [""important principles or tips""]
}}

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
                var knowledge = JsonSerializer.Deserialize<MusicTheoryKnowledge>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return knowledge ?? new MusicTheoryKnowledge();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract music theory knowledge, using empty result");
        }

        return new MusicTheoryKnowledge();
    }

    /// <summary>
    /// Generate a summary of the music theory document
    /// </summary>
    private async Task<string> GenerateSummaryAsync(
        string fullText,
        List<MusicTheoryKnowledge> extractedTheory,
        CancellationToken cancellationToken)
    {
        // Combine all extracted knowledge
        var allChordScales = extractedTheory.SelectMany(k => k.ChordScaleRelationships ?? []).Distinct().ToList();
        var allVoiceLeading = extractedTheory.SelectMany(k => k.VoiceLeadingRules ?? []).Distinct().ToList();
        var allProgressions = extractedTheory.SelectMany(k => k.HarmonicProgressions ?? []).Distinct().ToList();
        var allSubstitutions = extractedTheory.SelectMany(k => k.ChordSubstitutions ?? []).Distinct().ToList();

        var prompt = $@"Generate a concise summary (2-3 paragraphs) of this music theory document.

Extracted Knowledge:
- Chord-Scale Relationships: {string.Join(", ", allChordScales.Take(10))}
- Voice Leading Rules: {string.Join(", ", allVoiceLeading.Take(10))}
- Harmonic Progressions: {string.Join(", ", allProgressions.Take(10))}
- Chord Substitutions: {string.Join(", ", allSubstitutions.Take(10))}

Full text (first 1000 chars):
{fullText.Substring(0, Math.Min(1000, fullText.Length))}

Summary:";

        try
        {
            return await _ollamaChat.ChatAsync(prompt, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate summary, using default");
            return "Music theory document covering chord-scale relationships, voice leading, harmonic progressions, and theoretical concepts.";
        }
    }

    /// <summary>
    /// Store the processed music theory document in MongoDB
    /// </summary>
    private async Task<string> StoreMusicTheoryDocumentAsync(
        string documentId,
        string title,
        string content,
        string summary,
        List<MusicTheoryKnowledge> extractedTheory,
        List<float> embedding,
        string sourceType,
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        var database = typeof(MongoDbService)
            .GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_mongoDb) as IMongoDatabase
            ?? throw new InvalidOperationException("Could not access MongoDB database");

        var collection = database.GetCollection<BsonDocument>("music_theory_knowledge_base");

        var document = new BsonDocument
        {
            ["document_id"] = documentId,
            ["source_type"] = sourceType,
            ["source_url"] = sourceUrl ?? "",
            ["title"] = title,
            ["content"] = content,
            ["summary"] = summary,
            ["extracted_knowledge"] = new BsonDocument
            {
                ["chord_scale_relationships"] = new BsonArray(extractedTheory.SelectMany(k => k.ChordScaleRelationships ?? []).Distinct()),
                ["voice_leading_rules"] = new BsonArray(extractedTheory.SelectMany(k => k.VoiceLeadingRules ?? []).Distinct()),
                ["harmonic_progressions"] = new BsonArray(extractedTheory.SelectMany(k => k.HarmonicProgressions ?? []).Distinct()),
                ["chord_substitutions"] = new BsonArray(extractedTheory.SelectMany(k => k.ChordSubstitutions ?? []).Distinct()),
                ["modal_theory"] = new BsonArray(extractedTheory.SelectMany(k => k.ModalTheory ?? []).Distinct()),
                ["jazz_harmony_concepts"] = new BsonArray(extractedTheory.SelectMany(k => k.JazzHarmonyConcepts ?? []).Distinct()),
                ["functional_harmony"] = new BsonArray(extractedTheory.SelectMany(k => k.FunctionalHarmony ?? []).Distinct()),
                ["theoretical_concepts"] = new BsonArray(extractedTheory.SelectMany(k => k.TheoreticalConcepts ?? []).Distinct()),
                ["key_insights"] = new BsonArray(extractedTheory.SelectMany(k => k.KeyInsights ?? []).Distinct())
            },
            ["embedding"] = new BsonArray(embedding.Select(e => new BsonDouble(e))),
            ["processed_at"] = DateTime.UtcNow
        };

        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("document_id", documentId),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return documentId;
    }

    /// <summary>
    /// Chunk text into smaller pieces for processing
    /// </summary>
    private List<string> ChunkText(string text, int maxChunkSize, int overlap)
    {
        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new List<string>();
        var currentSize = 0;

        foreach (var word in words)
        {
            currentChunk.Add(word);
            currentSize += word.Length + 1;

            if (currentSize >= maxChunkSize)
            {
                chunks.Add(string.Join(' ', currentChunk));
                
                // Keep overlap words for next chunk
                var overlapWords = currentChunk.TakeLast(overlap / 10).ToList();
                currentChunk = overlapWords;
                currentSize = overlapWords.Sum(w => w.Length + 1);
            }
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(' ', currentChunk));
        }

        return chunks;
    }
}

/// <summary>
/// Result of music theory document processing
/// </summary>
public class MusicTheoryProcessingResult
{
    public bool Success { get; set; }
    public string? DocumentId { get; set; }
    public string? Summary { get; set; }
    public List<MusicTheoryKnowledge>? ExtractedTheory { get; set; }
    public int ChunkCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Structured music theory knowledge extracted from text
/// </summary>
public class MusicTheoryKnowledge
{
    public List<string>? ChordScaleRelationships { get; set; }
    public List<string>? VoiceLeadingRules { get; set; }
    public List<string>? HarmonicProgressions { get; set; }
    public List<string>? ChordSubstitutions { get; set; }
    public List<string>? ModalTheory { get; set; }
    public List<string>? JazzHarmonyConcepts { get; set; }
    public List<string>? FunctionalHarmony { get; set; }
    public List<string>? TheoreticalConcepts { get; set; }
    public List<string>? KeyInsights { get; set; }
}

