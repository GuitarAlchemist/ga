namespace GA.Data.MongoDB.Services.DocumentServices.Rag;

using Business.Core.Chords;
using Embeddings;
using Microsoft.Extensions.Logging;
using Models.Rag;
using Models.References;
using global::MongoDB.Driver;

/// <summary>
/// Enhanced chord RAG service with multi-stage processing
/// Stage 1: Extract chord information and generate summaries
/// Stage 2: Generate embeddings and enrich with knowledge graph data
/// </summary>
[UsedImplicitly]
public sealed class EnhancedChordRagService(
    ILogger<EnhancedChordRagService> logger,
    MongoDbService mongoDb,
    IEmbeddingService embeddingService)
    : MultiStageRagService<ChordRagEmbedding>(logger, mongoDb, embeddingService), IRagSyncService<ChordRagEmbedding>
{
    /// <summary>
    /// Stage 1: Analyze chord templates and extract basic information
    /// This is a lightweight operation that doesn't require LLM processing
    /// </summary>
    protected override async Task<List<ChordRagEmbedding>> AnalyzeAndSummarizeAsync(List<ChordRagEmbedding> documents)
    {
        Logger.LogInformation("Stage 1: Analyzing {Count} chord documents", documents.Count);

        // If documents are already provided, just validate and return
        if (documents.Count > 0)
        {
            return await Task.FromResult(documents);
        }

        // Generate from chord templates
        var chordTemplates = ChordTemplateFactory.GenerateAllPossibleChords();
        var analyzedDocuments = chordTemplates
            .Select(template => new ChordRagEmbedding
            {
                Name = template.PitchClassSet.Name,
                Root = template.PitchClassSet.Notes.First().ToString(),
                Quality = DetermineQuality(template),
                Intervals = template.PitchClassSet.Notes
                    .Skip(1)
                    .Select(n => template.PitchClassSet.Notes.First().GetInterval(n).ToString())
                    .ToList(),
                Notes = template.PitchClassSet.Notes.Select(n => n.ToString()).ToList(),
                RelatedScales = GetRelatedScales(template),
                CommonProgressions = GetCommonProgressions(template),
                CommonVoicings = GetCommonVoicings(template),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        Logger.LogInformation("Stage 1 complete: Analyzed {Count} chords", analyzedDocuments.Count);
        return await Task.FromResult(analyzedDocuments);
    }

    /// <summary>
    /// Stage 2: Deep processing with embedding generation
    /// This is an expensive operation that uses the embedding service
    /// </summary>
    protected override async Task<List<ChordRagEmbedding>> DeepProcessAsync(List<ChordRagEmbedding> documents)
    {
        Logger.LogInformation("Stage 2: Deep processing {Count} chord documents", documents.Count);

        // Generate search text and embeddings
        await GenerateEmbeddingsAsync(documents);

        // Enrich with additional knowledge graph data (future enhancement)
        // This could include:
        // - Harmonic function analysis
        // - Voice leading relationships
        // - Style-specific usage patterns
        // - Tension/resolution characteristics

        Logger.LogInformation("Stage 2 complete: Generated embeddings for {Count} chords", documents.Count);
        return documents;
    }

    protected override IMongoCollection<ChordRagEmbedding> GetCollection()
    {
        return MongoDb.ChordsRag;
    }

    /// <summary>
    /// Sync all chords using the multi-stage pipeline
    /// </summary>
    public async Task<bool> SyncAsync()
    {
        try
        {
            Logger.LogInformation("Starting enhanced chord RAG sync");

            // Start with empty list - AnalyzeAndSummarizeAsync will generate from templates
            var emptyList = new List<ChordRagEmbedding>();
            var success = await ProcessAsync(emptyList);

            if (success)
            {
                Logger.LogInformation("Enhanced chord RAG sync completed successfully");
            }
            else
            {
                Logger.LogWarning("Enhanced chord RAG sync completed with errors");
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in enhanced chord RAG sync");
            return false;
        }
    }

    #region Helper Methods (from original ChordSyncService)

    private static string DetermineQuality(ChordTemplate template)
    {
        var intervals = template.PitchClassSet.Notes
            .Skip(1)
            .Select(n => template.PitchClassSet.Notes.First().GetInterval(n))
            .ToList();

        // Determine quality based on intervals
        if (intervals.Any(i => i.Semitones == 3))
            return "Minor";
        if (intervals.Any(i => i.Semitones == 4))
            return "Major";
        if (intervals.Any(i => i.Semitones == 6))
            return "Diminished";
        if (intervals.Any(i => i.Semitones == 8))
            return "Augmented";

        return "Other";
    }

    private static List<ScaleReference> GetRelatedScales(ChordTemplate template)
    {
        // Simplified - in production, this would query a scale database
        // or use music theory rules to find compatible scales
        // ScaleReference(string Name, string Category, List<string> Notes)
        return
        [
            new ScaleReference(
                $"{template.PitchClassSet.Notes.First()} Major",
                "Diatonic",
                []
            ),
            new ScaleReference(
                $"{template.PitchClassSet.Notes.First()} Dorian",
                "Modal",
                []
            )
        ];
    }

    private static List<ProgressionReference> GetCommonProgressions(ChordTemplate template)
    {
        // Simplified - in production, this would analyze progression databases
        // ProgressionReference(string Name, List<string> Chords)
        return
        [
            new ProgressionReference(
                "ii-V-I",
                ["ii", "V", "I"]
            ),
            new ProgressionReference(
                "I-IV-V",
                ["I", "IV", "V"]
            )
        ];
    }

    private static List<VoicingReference> GetCommonVoicings(ChordTemplate template)
    {
        // Simplified - in production, this would query voicing databases
        // VoicingReference(string Name, List<string> Notes, string Instrument)
        return
        [
            new VoicingReference(
                "Root Position",
                [],
                "Guitar"
            ),
            new VoicingReference(
                "First Inversion",
                [],
                "Guitar"
            )
        ];
    }

    #endregion
}

