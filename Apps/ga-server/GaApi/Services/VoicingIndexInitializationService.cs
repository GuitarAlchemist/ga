namespace GaApi.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Fretboard.Voicings.Filtering;
using GA.Business.Core.Fretboard.Voicings.Generation;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Data.SemanticKernel.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only

/// <summary>
/// Background service that initializes the voicing index on application startup.
/// Extracted from VoicingSearchServiceExtensions to keep DI wiring focused.
/// </summary>
internal sealed class VoicingIndexInitializationService(
    VoicingIndexingService indexingService,
    EnhancedVoicingSearchService searchService,
    IBatchEmbeddingService batchEmbeddingService,
    IConfiguration configuration,
    ILogger<VoicingIndexInitializationService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!ShouldInitializeIndex())
                return;

            logger.LogInformation("Starting voicing index initialization...");

            var (generationTime, allVoicings) = GenerateVoicings();
            var (indexingTime, documentCount) = await IndexVoicingsAsync(allVoicings, stoppingToken);
            var (embeddingTime, allEmbeddings) = await LoadOrGenerateEmbeddingsAsync(documentCount, stoppingToken);
            await InitializeSearchServiceAsync(allEmbeddings, stoppingToken);

            LogCompletionStats(generationTime, indexingTime, embeddingTime, documentCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize voicing search index");
            // Allow host to continue even if initialization fails.
        }
    }

    private bool ShouldInitializeIndex()
    {
        var enableIndexing = configuration.GetValue<bool>("VoicingSearch:EnableIndexing", true);
        if (!enableIndexing)
        {
            logger.LogInformation("Voicing search indexing is disabled in configuration");
            return false;
        }

        var lazyLoading = configuration.GetValue<bool>("VoicingSearch:LazyLoading", false);
        if (lazyLoading)
        {
            logger.LogInformation("Voicing search lazy loading is enabled - index will be built on first search request");
            return false;
        }

        return true;
    }

    private (TimeSpan generationTime, IReadOnlyCollection<Voicing> voicings) GenerateVoicings()
    {
        logger.LogInformation("Generating voicings from fretboard...");
        var fretboard = Fretboard.Default;
        var minPlayedNotes = configuration.GetValue<int>("VoicingSearch:MinPlayedNotes", 2);

        var generationStart = DateTime.UtcNow;
        var allVoicings = VoicingGenerator.GenerateAllVoicings(
            fretboard,
            windowSize: 4,
            minPlayedNotes: minPlayedNotes,
            parallel: true);
        var generationTime = DateTime.UtcNow - generationStart;

        logger.LogInformation("Generated {Count} total voicings in {Seconds:F1}s",
            allVoicings.Count, generationTime.TotalSeconds);

        return (generationTime, allVoicings);
    }

    private async Task<(TimeSpan indexingTime, int documentCount)> IndexVoicingsAsync(
        IReadOnlyCollection<Voicing> allVoicings,
        CancellationToken stoppingToken)
    {
        var maxVoicings = configuration.GetValue<int>("VoicingSearch:MaxVoicingsToIndex", 1000);
        var noteCountFilterStr = configuration.GetValue<string>("VoicingSearch:NoteCountFilter", "ThreeNotes");
        var noteCountFilter = Enum.Parse<NoteCountFilter>(noteCountFilterStr);

        var vectorCollection = new RelativeFretVectorCollection(strCount: 6, fretExtent: 5);
        var criteria = new VoicingFilterCriteria
        {
            MaxResults = maxVoicings,
            NoteCount = noteCountFilter
        };

        logger.LogInformation("Indexing voicings with filters (MaxResults={MaxResults}, NoteCount={NoteCount})...",
            criteria.MaxResults, criteria.NoteCount);

        var indexingStart = DateTime.UtcNow;
        await indexingService.IndexFilteredVoicingsAsync(
            allVoicings,
            vectorCollection,
            criteria,
            stoppingToken);
        var indexingTime = DateTime.UtcNow - indexingStart;

        var documentCount = indexingService.Documents.Count;
        logger.LogInformation("Indexed {Count} voicing documents in {Seconds:F1}s",
            documentCount, indexingTime.TotalSeconds);

        return (indexingTime, documentCount);
    }

    private async Task<(TimeSpan embeddingTime, Dictionary<string, float[]> embeddings)> LoadOrGenerateEmbeddingsAsync(
        int documentCount,
        CancellationToken stoppingToken)
    {
        var embeddingStart = DateTime.UtcNow;
        var documents = indexingService.Documents.ToList();

        var cacheFile = GetCacheFilePath(documentCount);
        var allEmbeddings = await LoadOrGenerateEmbeddingsFromCacheAsync(cacheFile, documents, embeddingStart, stoppingToken);

        var embeddingTime = DateTime.UtcNow - embeddingStart;
        logger.LogInformation("Generated embeddings in {Seconds:F1}s ({MsPerEmbedding:F1}ms per embedding)",
            embeddingTime.TotalSeconds, embeddingTime.TotalMilliseconds / Math.Max(1, documentCount));

        return (embeddingTime, allEmbeddings);
    }

    private static string GetCacheFilePath(int documentCount)
    {
        var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "cache", "embeddings");
        Directory.CreateDirectory(cacheDir);
        return Path.Combine(cacheDir, $"voicing_embeddings_{documentCount}.bin");
    }

    private async Task<Dictionary<string, float[]>> LoadOrGenerateEmbeddingsFromCacheAsync(
        string cacheFile,
        List<VoicingDocument> documents,
        DateTime startTime,
        CancellationToken stoppingToken)
    {
        if (File.Exists(cacheFile))
        {
            logger.LogInformation("Loading embeddings from cache: {CacheFile}", Path.GetFileName(cacheFile));
            try
            {
                var embeddings = LoadEmbeddingsFromCache(cacheFile, documents);
                var loadTime = DateTime.UtcNow - startTime;
                logger.LogInformation("✓ Loaded {Count} embeddings from cache in {Seconds:F2}s",
                    embeddings.Count, loadTime.TotalSeconds);
                return embeddings;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load cache, regenerating embeddings...");
            }
        }
        else
        {
            logger.LogInformation("No cache found, generating embeddings using batch Ollama service...");
        }

        var allEmbeddings = await GenerateEmbeddingsAsync(documents, batchEmbeddingService, startTime, logger, stoppingToken);
        SaveEmbeddingsToCache(cacheFile, allEmbeddings, logger);
        return allEmbeddings;
    }

    private async Task InitializeSearchServiceAsync(
        Dictionary<string, float[]> allEmbeddings,
        CancellationToken stoppingToken)
    {
        var documents = indexingService.Documents.ToList();
        logger.LogInformation("Initializing search service with {Count} pre-generated embeddings...", allEmbeddings.Count);

        var textToIdsMap = documents
            .GroupBy(d => d.SearchableText)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Id).ToList());

        await searchService.InitializeEmbeddingsAsync(
            async text =>
            {
                if (textToIdsMap.TryGetValue(text, out var docIds) && docIds.Count > 0)
                {
                    var docId = docIds[0];
                    if (allEmbeddings.TryGetValue(docId, out var embedding))
                    {
                        return [.. embedding.Select(f => (double)f)];
                    }
                }

                logger.LogWarning("Embedding not found for text '{Text}', generating on-demand",
                    text[..Math.Min(50, text.Length)]);
                var result = await batchEmbeddingService.GenerateBatchEmbeddingsAsync(new[] { text }, stoppingToken);
                return [.. result[0].Select(f => (double)f)];
            },
            stoppingToken);
    }

    private void LogCompletionStats(
        TimeSpan generationTime,
        TimeSpan indexingTime,
        TimeSpan embeddingTime,
        int documentCount)
    {
        var totalTime = generationTime + indexingTime + embeddingTime;
        logger.LogInformation("Voicing search index initialized successfully with {Count} voicings in {TotalSeconds:F1}s total",
            documentCount, totalTime.TotalSeconds);

        var stats = searchService.GetStats();
        logger.LogInformation(
            "Voicing search stats: {TotalVoicings} voicings, {MemoryMB} MB memory, {AvgSearchTime}ms avg search time",
            stats.TotalVoicings,
            stats.MemoryUsageMb,
            stats.AverageSearchTime.TotalMilliseconds);
    }

    private static async Task<Dictionary<string, float[]>> GenerateEmbeddingsAsync(
        List<VoicingDocument> documents,
        IBatchEmbeddingService batchEmbeddingService,
        DateTime startTime,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var uniqueTexts = documents.Select(d => d.SearchableText).Distinct().ToArray();
        logger.LogInformation("Generating embeddings for {UniqueCount} unique texts (from {TotalCount} documents)...",
            uniqueTexts.Length, documents.Count);

        var textEmbeddings = new Dictionary<string, float[]>(uniqueTexts.Length);
        const int batchSize = 500;

        for (var i = 0; i < uniqueTexts.Length; i += batchSize)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var batch = uniqueTexts.Skip(i).Take(batchSize).ToArray();
            var batchEmbeddings = await batchEmbeddingService.GenerateBatchEmbeddingsAsync(batch, cancellationToken);

            for (var j = 0; j < batch.Length; j++)
            {
                textEmbeddings[batch[j]] = batchEmbeddings[j];
            }

            var processed = Math.Min(i + batchSize, uniqueTexts.Length);
            var percentage = (processed * 100.0) / uniqueTexts.Length;
            var elapsed = DateTime.UtcNow - startTime;
            var rate = processed / Math.Max(0.1, elapsed.TotalSeconds);
            var remaining = rate > 0 ? (uniqueTexts.Length - processed) / rate : double.PositiveInfinity;

            logger.LogInformation(
                "Embedding progress: {Current}/{Total} ({Percentage:F1}%) - {Rate:F1} embeddings/sec - ETA: {Remaining:F0}s",
                processed, uniqueTexts.Length, percentage, rate, double.IsInfinity(remaining) ? 0 : remaining);
        }

        logger.LogInformation("Text embeddings dictionary has {Count} entries", textEmbeddings.Count);

        var allEmbeddings = new Dictionary<string, float[]>(documents.Count);
        var missingCount = 0;
        foreach (var doc in documents)
        {
            if (!textEmbeddings.TryGetValue(doc.SearchableText, out var embedding))
            {
                if (missingCount < 5)
                {
                    logger.LogWarning("Missing embedding for document {Id} with text length {Length}: '{Text}'",
                        doc.Id, doc.SearchableText.Length, doc.SearchableText[..Math.Min(100, doc.SearchableText.Length)]);
                }

                missingCount++;
                continue;
            }

            allEmbeddings[doc.Id] = embedding;
        }

        if (missingCount > 0)
        {
            logger.LogWarning("Total missing embeddings: {Count} out of {Total}", missingCount, documents.Count);
        }

        logger.LogInformation("Mapped {Count} embeddings from {UniqueCount} unique texts to {DocumentCount} documents",
            allEmbeddings.Count, textEmbeddings.Count, documents.Count);

        return allEmbeddings;
    }

    private static void SaveEmbeddingsToCache(
        string cacheFile,
        Dictionary<string, float[]> embeddings,
        ILogger logger)
    {
        try
        {
            using var fs = new FileStream(cacheFile, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);

            writer.Write(embeddings.Count);
            writer.Write(embeddings.First().Value.Length);

            foreach (var (id, embedding) in embeddings)
            {
                writer.Write(id);
                foreach (var value in embedding)
                {
                    writer.Write(value);
                }
            }

            logger.LogInformation("✓ Saved {Count} embeddings to cache: {CacheFile}",
                embeddings.Count, Path.GetFileName(cacheFile));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save embeddings to cache");
        }
    }

    private static Dictionary<string, float[]> LoadEmbeddingsFromCache(
        string cacheFile,
        List<VoicingDocument> documents)
    {
        using var fs = new FileStream(cacheFile, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        var count = reader.ReadInt32();
        var dimension = reader.ReadInt32();

        if (count != documents.Count)
        {
            throw new InvalidOperationException(
                $"Cache count mismatch: expected {documents.Count}, got {count}");
        }

        var embeddings = new Dictionary<string, float[]>(count);
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadString();
            var embedding = new float[dimension];
            for (var j = 0; j < dimension; j++)
            {
                embedding[j] = reader.ReadSingle();
            }

            embeddings[id] = embedding;
        }

        return embeddings;
    }
}
#pragma warning restore SKEXP0001
