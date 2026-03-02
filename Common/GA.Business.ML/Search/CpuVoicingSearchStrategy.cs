namespace GA.Business.ML.Search;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GA.Domain.Services.Fretboard.Voicings.Core;
using Rag.Models;

/// <summary>
/// Fast, pure C# in-memory search strategy for voicing retrieval.
/// Used for integration tests and as a fallback when GPU is unavailable.
/// Implements Weighted Partition Cosine Similarity for OPTIC-K Schema v1.3.1.
/// Supports legacy OPTIC-K Schema v1.2.1.
/// </summary>
public class CpuVoicingSearchStrategy : IVoicingSearchStrategy
{
    private readonly Dictionary<string, VoicingEmbedding> _voicings = [];
    private long _totalSearches;
    private TimeSpan _totalSearchTime = TimeSpan.Zero;
    private readonly object _statsLock = new();

    // OPTIC-K Schema Constants
    private const int OpticKv17Dim = 228;
    private const int OpticKv16Dim = 216;
    private const int OpticKv131Dim = 109;
    private const int LegacyDim = 96;

    // Partition Config
    private const int StructureOffset = 6;
    private const int StructureDim = 24;
    private const int MorphologyOffset = 30;
    private const int MorphologyDim = 24;
    private const int ContextOffset = 54;
    private const int ContextDim = 12;
    private const int SymbolicOffset = 66;
    private const int SymbolicDim = 12;

    // Weights
    private const double StructureWeight = 0.45;
    private const double MorphologyWeight = 0.25;
    private const double ContextWeight = 0.20;
    private const double SymbolicWeight = 0.10;

    public string Name => "CPU-Parallel";
    public bool IsAvailable => true;

    public VoicingSearchPerformance Performance => new(
        TimeSpan.FromMilliseconds(5.0),
        CalculateMemoryUsage(),
        false,
        false);

    public Task InitializeAsync(IEnumerable<VoicingEmbedding> voicings)
    {
        _voicings.Clear();
        foreach (var v in voicings)
        {
            _voicings[v.Id] = v;
        }
        return Task.CompletedTask;
    }

    public async Task<List<VoicingSearchResult>> SemanticSearchAsync(double[] queryEmbedding, int limit = 10)
    {
        var stopwatch = Stopwatch.StartNew();

        var results = new ConcurrentBag<VoicingSearchResult>();
        var voicingList = _voicings.Values.ToList();

        await Task.Run(() => {
            Parallel.ForEach(voicingList, voicing => {
                var targetVec = voicing.TextEmbedding ?? voicing.Embedding;
                var similarity = CosineSimilarity(queryEmbedding, targetVec);
                results.Add(MapToSearchResult(voicing, similarity, "semantic search"));
            });
        });

        stopwatch.Stop();
        RecordSearchTime(stopwatch.Elapsed);

        return [.. results.OrderByDescending(r => r.Score).Take(limit)];
    }

    public async Task<List<VoicingSearchResult>> FindSimilarVoicingsAsync(string voicingId, int limit = 10)
    {
        if (!_voicings.TryGetValue(voicingId, out var target)) return [];

        var results = await SemanticSearchAsync(target.Embedding, limit + 1);
        return [.. results.Where(r => r.Document.Id != voicingId).Take(limit)];
    }

    public async Task<List<VoicingSearchResult>> HybridSearchAsync(double[] queryEmbedding, VoicingSearchFilters filters, int limit = 10)
    {
        var stopwatch = Stopwatch.StartNew();

        var results = new ConcurrentBag<VoicingSearchResult>();
        var voicingList = _voicings.Values.ToList();

        await Task.Run(() => {
            Parallel.ForEach(voicingList, voicing => {
                if (MatchesFilters(voicing, filters))
                {
                    // If target has text embedding, use it for semantic alignment
                    // otherwise fall back to musical embedding
                    var targetVec = voicing.TextEmbedding ?? voicing.Embedding;
                    var score = CosineSimilarity(queryEmbedding, targetVec);

                    // Phase 7: Symbolic Boosting
                    // If query specifically targeted certain bits (e.g. "beginner", "hendrix"),
                    // boost voicings that have those bits set.
                    if (filters.SymbolicBitIndices != null && filters.SymbolicBitIndices.Length > 0)
                    {
                        const int symbolicOffset = 66; // From EmbeddingSchema
                        var matches = 0;

                        foreach (var bit in filters.SymbolicBitIndices)
                        {
                            if (bit < 12 && voicing.Embedding.Length > symbolicOffset + bit)
                            {
                                if (voicing.Embedding[symbolicOffset + bit] > 0.5)
                                {
                                    matches++;
                                }
                            }
                        }

                        if (matches > 0)
                        {
                            // Apply 20% boost per match, capped at 2x score
                            score *= (1.0 + (matches * 0.2));
                            if (score > 1.0) score = 1.0; // Keep in 0-1 range for ranking
                        }
                    }

                    results.Add(MapToSearchResult(voicing, score, "hybrid search"));
                }
            });
        });

        stopwatch.Stop();
        RecordSearchTime(stopwatch.Elapsed);

        return [.. results.OrderByDescending(r => r.Score).Take(limit)];
    }

    public VoicingSearchStats GetStats() =>
        new(
            _voicings.Count,
            CalculateMemoryUsage(),
            _totalSearches > 0 ? TimeSpan.FromTicks(_totalSearchTime.Ticks / _totalSearches) : TimeSpan.Zero,
            _totalSearches);

    private static bool MatchesFilters(VoicingEmbedding voicing, VoicingSearchFilters filters)
    {
        if (filters.Difficulty != null && !voicing.Difficulty.Equals(filters.Difficulty, StringComparison.OrdinalIgnoreCase)) return false;
        if (filters.Position != null && !voicing.Position.Equals(filters.Position, StringComparison.OrdinalIgnoreCase)) return false;
        if (filters.VoicingType != null && !voicing.VoicingType.Contains(filters.VoicingType, StringComparison.OrdinalIgnoreCase)) return false;
        if (filters.Tags != null && filters.Tags.Any() && !filters.Tags.All(t => voicing.SemanticTags.Contains(t, StringComparer.OrdinalIgnoreCase))) return false;

        if (filters.MinFret.HasValue && voicing.MinFret < filters.MinFret.Value) return false;
        if (filters.MaxFret.HasValue && voicing.MaxFret > filters.MaxFret.Value) return false;
        if (filters.RequireBarreChord.HasValue && voicing.BarreRequired != filters.RequireBarreChord.Value) return false;

        // Structured Filters
        if (filters.ChordName != null && !voicing.ChordName.Contains(filters.ChordName, StringComparison.OrdinalIgnoreCase)) return false;

        if (filters.StackingType != null)
        {
            // Null check on voicing.StackingType because it's nullable
            if (voicing.StackingType == null || !voicing.StackingType.Equals(filters.StackingType, StringComparison.OrdinalIgnoreCase)) return false;
        }

        if (filters.IsSlashChord.HasValue)
        {
            var isSlash = (voicing.MidiBassNote % 12) != voicing.RootPitchClass;
            if (filters.IsSlashChord.Value != isSlash) return false;
        }

        if (filters.MinMidiPitch.HasValue && voicing.MidiNotes.Length > 0 && voicing.MidiNotes.Min() < filters.MinMidiPitch.Value) return false;
        if (filters.MaxMidiPitch.HasValue && voicing.MidiNotes.Length > 0 && voicing.MidiNotes.Max() > filters.MaxMidiPitch.Value) return false;

        if (filters.SetClassId != null && !voicing.PrimeFormId.Contains(filters.SetClassId, StringComparison.OrdinalIgnoreCase)) return false;

        // PitchClassSet string looks like "{0,4,7}" or similar.
        if (filters.RahnPrimeForm != null && !voicing.PitchClassSet.Contains(filters.RahnPrimeForm, StringComparison.OrdinalIgnoreCase)) return false;

        if (filters.FingerCount.HasValue)
        {
            // Heuristic: Count non-open, non-muted strings.
            // Perfect finger count requires fingering analysis which is not in the search index yet.
            var parts = voicing.Diagram.Contains('-') ? voicing.Diagram.Split('-') : [.. voicing.Diagram.Select(c => c.ToString())];
            var active = parts.Count(p => p != "x" && p != "m" && p != "0");

            // Adjust for barre: if barre is required, typically reduces finger count by count of barred notes - 1
            // But without exact data, we test against active strings for now as a proxy.
            if (active != filters.FingerCount.Value) return false;
        }

        // Phase 3 Extended Filters
        if (filters.HarmonicFunction != null && !string.Equals(voicing.HarmonicFunction, filters.HarmonicFunction, StringComparison.OrdinalIgnoreCase)) return false;
        if (filters.IsNaturallyOccurring.HasValue && voicing.IsNaturallyOccurring != filters.IsNaturallyOccurring.Value) return false;
        if (filters.IsRootless.HasValue && voicing.IsRootless != filters.IsRootless.Value) return false;
        if (filters.HasGuideTones.HasValue && voicing.HasGuideTones != filters.HasGuideTones.Value) return false;
        if (filters.Inversion.HasValue && voicing.Inversion != filters.Inversion.Value) return false;
        if (filters.MinConsonance.HasValue && voicing.ConsonanceScore < filters.MinConsonance.Value) return false;
        if (filters.MinBrightness.HasValue && voicing.BrightnessScore < filters.MinBrightness.Value) return false;
        if (filters.MaxBrightness.HasValue && voicing.BrightnessScore > filters.MaxBrightness.Value) return false;
        if (filters.MaxBrightness.HasValue && voicing.BrightnessScore > filters.MaxBrightness.Value) return false;
        if (filters.OmittedTones != null && filters.OmittedTones.Any() && !filters.OmittedTones.All(t => voicing.OmittedTones.Contains(t, StringComparer.OrdinalIgnoreCase))) return false;

        // Melody Note Filter
        if (filters.TopPitchClass.HasValue && voicing.TopPitchClass != filters.TopPitchClass.Value) return false;

        // AI Agent Metadata Filters (Phase 4)
        if (filters.TexturalDescriptionContains != null &&
            (voicing.TexturalDescription == null || !voicing.TexturalDescription.Contains(filters.TexturalDescriptionContains, StringComparison.OrdinalIgnoreCase))) return false;

        if (filters.DoubledTonesContain != null && filters.DoubledTonesContain.Any() &&
            (voicing.DoubledTones == null || !filters.DoubledTonesContain.All(t => voicing.DoubledTones.Contains(t, StringComparer.OrdinalIgnoreCase)))) return false;

        if (filters.AlternateNameMatch != null &&
            (voicing.AlternateNames == null || !voicing.AlternateNames.Any(n => n.Contains(filters.AlternateNameMatch, StringComparison.OrdinalIgnoreCase)))) return false;

        return true;
    }

    private static double CosineSimilarity(double[] v1, double[] v2)
    {
        if (v1.Length != v2.Length) return 0.0;

        // Use weighted partition similarity for musical embeddings (v1.7, v1.6, v1.3.1, v1.2.1)
        if (v1.Length == OpticKv17Dim || v1.Length == OpticKv16Dim || v1.Length == OpticKv131Dim || v1.Length == LegacyDim)
        {
            return CalculateMusicalSimilarity(v1, v2);
        }

        // Fallback to standard Cosine Similarity
        return System.Numerics.Tensors.TensorPrimitives.CosineSimilarity(v1, v2);
    }

    private static double CalculateMusicalSimilarity(double[] query, double[] target)
    {
        double score = 0;

        // STRUCTURE
        score += StructureWeight * ComputePartitionCosine(query, target, StructureOffset, StructureDim);

        // MORPHOLOGY
        score += MorphologyWeight * ComputePartitionCosine(query, target, MorphologyOffset, MorphologyDim);

        // CONTEXT
        score += ContextWeight * ComputePartitionCosine(query, target, ContextOffset, ContextDim);

        // SYMBOLIC
        score += SymbolicWeight * ComputePartitionCosine(query, target, SymbolicOffset, SymbolicDim);

        return score;
    }

    private static double ComputePartitionCosine(double[] v1, double[] v2, int offset, int dim)
    {
        // Using Span for performance to avoid allocations
        var span1 = new ReadOnlySpan<double>(v1, offset, dim);
        var span2 = new ReadOnlySpan<double>(v2, offset, dim);
        
        return System.Numerics.Tensors.TensorPrimitives.CosineSimilarity(span1, span2);
    }

    private static VoicingSearchResult MapToSearchResult(VoicingEmbedding voicing, double score, string query)
    {
        var document = new ChordVoicingRagDocument
        {
            Id = voicing.Id,
            SearchableText = voicing.Description,
            ChordName = voicing.ChordName,
            VoicingType = voicing.VoicingType,
            Position = voicing.Position,
            Difficulty = voicing.Difficulty,
            ModeName = voicing.ModeName,
            ModalFamily = voicing.ModalFamily,
            SemanticTags = voicing.SemanticTags,
            PossibleKeys = voicing.PossibleKeys,
            PrimeFormId = voicing.PrimeFormId,
            TranslationOffset = voicing.TranslationOffset,
            YamlAnalysis = voicing.Description,
            Diagram = voicing.Diagram,
            MidiNotes = voicing.MidiNotes,
            PitchClasses = [.. voicing.MidiNotes.Select(n => n % 12).Distinct().OrderBy(p => p)],
            PitchClassSet = voicing.PitchClassSet,
            IntervalClassVector = voicing.IntervalClassVector,
            MinFret = voicing.MinFret,
            MaxFret = voicing.MaxFret,
            BarreRequired = voicing.BarreRequired,
            HandStretch = voicing.HandStretch,

            AnalysisEngine = "CpuVoicingSearchStrategy",
            AnalysisVersion = "1.0.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = voicing.PrimeFormId,
            StackingType = voicing.StackingType,
            RootPitchClass = voicing.RootPitchClass,
            MidiBassNote = voicing.MidiBassNote,
            HarmonicFunction = voicing.HarmonicFunction,
            IsNaturallyOccurring = voicing.IsNaturallyOccurring,
            Consonance = voicing.ConsonanceScore,
            Brightness = voicing.BrightnessScore,
            IsRootless = voicing.IsRootless,
            HasGuideTones = voicing.HasGuideTones,
            Inversion = voicing.Inversion,
            TopPitchClass = voicing.TopPitchClass, // Added for Chord Melody support
            OmittedTones = voicing.OmittedTones,

            // AI Agent Metadata
            TexturalDescription = voicing.TexturalDescription,
            DoubledTones = voicing.DoubledTones,
            AlternateNames = voicing.AlternateNames,
            CagedShape = voicing.CagedShape, // Map CAGED shape

            DifficultyScore = 1.0 // Simple default
        };

        return new(document, score, query);
    }

    private long CalculateMemoryUsage()
    {
        if (_voicings.Count == 0) return 0;
        var first = _voicings.Values.First();
        return (_voicings.Count * (first.Embedding.Length * sizeof(double))) / (1024 * 1024);
    }

    private void RecordSearchTime(TimeSpan elapsed)
    {
        Interlocked.Increment(ref _totalSearches);
        lock (_statsLock)
        {
            _totalSearchTime = _totalSearchTime.Add(elapsed);
        }
    }
}
