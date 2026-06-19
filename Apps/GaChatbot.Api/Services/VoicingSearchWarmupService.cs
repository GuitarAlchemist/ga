namespace GaChatbot.Api.Services;

using GA.Business.ML.Embeddings;
using GA.Business.ML.Rag.Models;
using GA.Business.ML.Search;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Services.Fretboard.Voicings.Filtering;
using GA.Domain.Services.Fretboard.Voicings.Generation;

public sealed class VoicingSearchWarmupService(
    IConfiguration configuration,
    VoicingIndexingService indexingService,
    EnhancedVoicingSearchService searchService,
    MusicalEmbeddingGenerator embeddingGenerator,
    ILogger<VoicingSearchWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting thin-host voicing search warmup...");

            var fretboard = Fretboard.Default;
            var minPlayedNotes = configuration.GetValue("VoicingSearch:MinPlayedNotes", 2);
            var maxVoicings = configuration.GetValue("VoicingSearch:MaxVoicingsToIndex", 4000);
            var noteCountFilterStr = configuration.GetValue<string>("VoicingSearch:NoteCountFilter", "All");
            var noteCountFilter = Enum.TryParse<NoteCountFilter>(noteCountFilterStr, out var parsed)
                ? parsed
                : NoteCountFilter.All;

            var allVoicings = VoicingGenerator.GenerateAllVoicings(
                fretboard,
                4,
                minPlayedNotes,
                true);

            var vectorCollection = new RelativeFretVectorCollection(6, 5);
            var criteria = new VoicingFilterCriteria
            {
                MaxResults = maxVoicings,
                NoteCount = noteCountFilter
            };

            var result = await indexingService.IndexFilteredVoicingsAsync(
                allVoicings,
                vectorCollection,
                criteria,
                stoppingToken);

            if (!result.Success || indexingService.Documents.Count == 0)
            {
                logger.LogWarning("Voicing warmup produced no indexed documents.");
                return;
            }

            await searchService.InitializeEmbeddingsAsync(
                semanticEmbeddingGenerator: _ => Task.FromResult(Array.Empty<double>()),
                musicalEmbeddingGenerator: async doc =>
                {
                    var embedding = await embeddingGenerator.GenerateEmbeddingAsync(ToEmbeddingDoc(doc));
                    return [.. embedding.Select(x => (double)x)];
                },
                cancellationToken: stoppingToken);

            logger.LogInformation(
                "Voicing search warmup completed. Indexed {Count} documents using {Strategy}.",
                indexingService.DocumentCount,
                searchService.StrategyName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voicing search warmup failed.");
        }
    }

    private static ChordVoicingRagDocument ToEmbeddingDoc(ChordVoicingRagDocument doc) => doc;
}
