namespace GA.Business.Core.Fretboard.Voicings.Search;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core;

/// <summary>
/// Enhanced voicing search service with support for multiple search strategies
/// including GPU-accelerated ILGPU and in-memory search
/// </summary>
public class EnhancedVoicingSearchService(
    VoicingIndexingService indexingService,
    IVoicingSearchStrategy searchStrategy)
{
    /// <summary>
    /// Gets the name of the current search strategy
    /// </summary>
    public string StrategyName => searchStrategy.Name;

    /// <summary>
    /// Gets whether the search strategy is available
    /// </summary>
    public bool IsAvailable => searchStrategy.IsAvailable;

    /// <summary>
    /// Gets whether the service is initialized
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the number of indexed documents
    /// </summary>
    public int DocumentCount => indexingService.DocumentCount;

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    public VoicingSearchPerformance Performance => searchStrategy.Performance;

    /// <summary>
    /// Computes embeddings for all indexed documents to prepare for semantic search.
    /// </summary>
    /// <param name="semanticEmbeddingGenerator">Function to generate embeddings from text (e.g. BERT/ONNX)</param>
    /// <param name="musicalEmbeddingGenerator">Optional function to generate musical feature embeddings (78-dim)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InitializeEmbeddingsAsync(
        Func<string, Task<double[]>> semanticEmbeddingGenerator,
        Func<VoicingDocument, Task<double[]>>? musicalEmbeddingGenerator = null,
        CancellationToken cancellationToken = default)
    {
        if (!searchStrategy.IsAvailable)
            throw new InvalidOperationException($"Search strategy '{searchStrategy.Name}' is not available");

        var documents = indexingService.Documents;
        var voicingEmbeddings = new List<VoicingEmbedding>(documents.Count);

        // Process all embeddings in parallel for maximum speed
        var embeddingTasks = documents.Select(async doc =>
        {
            // Use cached embeddings if available, otherwise generate
            var musicalEmbedding = doc.Embedding;
            if (musicalEmbedding == null && musicalEmbeddingGenerator != null)
            {
                musicalEmbedding = await musicalEmbeddingGenerator(doc);
            }
            
            var textEmbedding = doc.TextEmbedding;
            if (textEmbedding == null)
            {
                textEmbedding = await semanticEmbeddingGenerator(doc.SearchableText);
            }

            return new VoicingEmbedding(
                doc.Id,
                doc.ChordName ?? "Unknown",
                doc.VoicingType,
                doc.Position,
                doc.Difficulty,
                doc.ModeName,
                doc.ModalFamily,
                doc.PossibleKeys,
                doc.SemanticTags,
                doc.PrimeFormId ?? "",
                doc.TranslationOffset,
                doc.Diagram,
                doc.MidiNotes,
                doc.PitchClassSet,
                doc.IntervalClassVector,
                doc.MinFret,
                doc.MaxFret,
                doc.BarreRequired,
                doc.HandStretch,
                doc.StackingType,
                doc.RootPitchClass,
                doc.MidiBassNote,
                doc.HarmonicFunction,
                doc.IsNaturallyOccurring,
                doc.Consonance,
                doc.Brightness,
                doc.IsRootless,
                doc.HasGuideTones,
                doc.Inversion,
                GetTopPitchClass(doc), // Added for Chord Melody support
                doc.TexturalDescription, // Added for AI Agents
                doc.DoubledTones, // Added for AI Agents
                doc.AlternateNames, // Added for AI Agents
                doc.OmittedTones ?? [],
                doc.CagedShape, // Pass CAGED shape
                doc.YamlAnalysis,
                musicalEmbedding ?? new double[78], // Fallback to empty if still null
                textEmbedding);
        }).ToList();

        // Wait for all embeddings to complete
        var results = await Task.WhenAll(embeddingTasks);
        voicingEmbeddings.AddRange(results);

        await searchStrategy.InitializeAsync(voicingEmbeddings);
        IsInitialized = true;
    }

    /// <summary>
    /// Search for voicings using natural language query
    /// </summary>
    public async Task<List<VoicingSearchResult>> SearchAsync(
        string query,
        Func<string, Task<double[]>> embeddingGenerator,
        int topK = 10,
        VoicingSearchFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var queryEmbedding = await embeddingGenerator(query);
        
        // Phase 7: Parse symbolic intent
        var parser = new SymbolicQueryParser();
        var symbolicIndices = parser.ParseQueryTags(query);
        
        if (symbolicIndices.Any())
        {
            filters ??= new VoicingSearchFilters();
            filters = filters with { SymbolicBitIndices = [.. symbolicIndices] };
        }

        if (filters != null)
        {
            return await searchStrategy.HybridSearchAsync(queryEmbedding, filters, topK);
        }

        return await searchStrategy.SemanticSearchAsync(queryEmbedding, topK);
    }

    /// <summary>
    /// Find voicings similar to a given voicing
    /// </summary>
    public async Task<List<VoicingSearchResult>> FindSimilarAsync(
        string voicingId,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        return await searchStrategy.FindSimilarVoicingsAsync(voicingId, topK);
    }

    /// <summary>
    /// Get search statistics
    /// </summary>
    public VoicingSearchStats GetStats()
    {
        return searchStrategy.GetStats();
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Service not initialized. Call InitializeEmbeddingsAsync first.");
    }

    private int? GetTopPitchClass(VoicingDocument doc)
    {
        // 1. Prefer explicit property
        if (doc.TopPitchClass.HasValue) 
            return doc.TopPitchClass;

        // 2. Fallback to computing from MidiNotes if available
        if (doc.MidiNotes is { Length: > 0 })
        {
            var maxNote = doc.MidiNotes.Max();
            return maxNote % 12;
        }

        return null;
    }
}
