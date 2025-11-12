namespace GA.Business.Core.Fretboard.Voicings.Search;

using System.Diagnostics;
using Analysis;
using Core;
using Filtering;
using GA.Business.Core.Fretboard.Positions;
using Generation;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for indexing guitar voicings into a vector store for semantic search
/// </summary>
public class VoicingIndexingService
{
    private readonly ILogger<VoicingIndexingService> _logger;
    private readonly List<VoicingDocument> _indexedDocuments = new();

    public VoicingIndexingService(ILogger<VoicingIndexingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the count of indexed documents
    /// </summary>
    public int DocumentCount => _indexedDocuments.Count;

    /// <summary>
    /// Gets all indexed documents
    /// </summary>
    public IReadOnlyList<VoicingDocument> Documents => _indexedDocuments.AsReadOnly();

    /// <summary>
    /// Index all voicings from a collection, using only prime forms to avoid duplicates
    /// </summary>
    public async Task<VoicingIndexingResult> IndexVoicingsAsync(
        IEnumerable<Voicing> allVoicings,
        RelativeFretVectorCollection vectorCollection,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting voicing indexing...");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _indexedDocuments.Clear();

            // Decompose voicings to get equivalence groups
            _logger.LogInformation("Decomposing voicings into equivalence groups...");
            var voicingsList = allVoicings.ToList();
            var decomposed = VoicingDecomposer.DecomposeVoicings(voicingsList, vectorCollection).ToList();
            _logger.LogInformation("Decomposed {Count} voicings", decomposed.Count);

            // Filter to only prime forms to avoid indexing duplicates
            var primeFormsOnly = decomposed.Where(d => d.PrimeForm != null).ToList();
            _logger.LogInformation("Found {Count} unique prime forms", primeFormsOnly.Count);

            var processedCount = 0;
            var errorCount = 0;

            // Use parallel processing for better performance
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            var documentsLock = new object();
            var progressLock = new object();

            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(primeFormsOnly, parallelOptions, decomposedVoicing =>
                    {
                        try
                        {
                            // Analyze the voicing with enhanced metadata
                            var analysis = VoicingAnalyzer.AnalyzeEnhanced(decomposedVoicing);

                            // Create document
                            var document = VoicingDocument.FromAnalysis(
                                decomposedVoicing.Voicing,
                                analysis,
                                decomposedVoicing.PrimeForm?.ToString(),
                                0); // Prime forms have 0 translation offset

                            lock (documentsLock)
                            {
                                _indexedDocuments.Add(document);
                            }

                            lock (progressLock)
                            {
                                processedCount++;
                                if (processedCount % 1000 == 0)
                                {
                                    _logger.LogInformation("Processed {Count} voicings...", processedCount);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing voicing {Voicing}", decomposedVoicing.Voicing);
                            lock (progressLock)
                            {
                                errorCount++;
                            }
                        }
                    });
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Indexing cancelled after processing {Count} voicings", processedCount);
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "Voicing indexing complete: {Count} documents indexed in {ElapsedMs}ms ({ErrorCount} errors)",
                processedCount,
                stopwatch.ElapsedMilliseconds,
                errorCount);

            return new VoicingIndexingResult(
                Success: true,
                DocumentCount: processedCount,
                Duration: stopwatch.Elapsed,
                ErrorCount: errorCount,
                Message: $"Successfully indexed {processedCount} voicings ({errorCount} errors)");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to index voicings");

            return new VoicingIndexingResult(
                Success: false,
                DocumentCount: 0,
                Duration: stopwatch.Elapsed,
                ErrorCount: 0,
                Message: $"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Index a filtered subset of voicings based on criteria (optimized streaming approach)
    /// </summary>
    public async Task<VoicingIndexingResult> IndexFilteredVoicingsAsync(
        IEnumerable<Voicing> allVoicings,
        RelativeFretVectorCollection vectorCollection,
        VoicingFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting filtered voicing indexing with criteria: {Criteria}", criteria);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _indexedDocuments.Clear();

            var processedCount = 0;
            var errorCount = 0;
            var filteredCount = 0;
            var decomposedCount = 0;

            // Convert vectorCollection to array for O(1) access
            var vectorArray = vectorCollection.ToArray();
            var variations = new GA.Core.Combinatorics.VariationsWithRepetitions<GA.Business.Core.Fretboard.Primitives.RelativeFret>(
                GA.Business.Core.Fretboard.Primitives.RelativeFret.Range(0, 5),
                length: 6);

            // Use parallel processing with early termination support
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            var documentsLock = new object();
            var progressLock = new object();
            var shouldStop = false;

            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(allVoicings, parallelOptions, (voicing, state) =>
                    {
                        if (shouldStop)
                        {
                            state.Stop();
                            return;
                        }

                        try
                        {
                            // OPTIMIZATION 1: Early filtering by note count (before decomposition)
                            var playedNotes = voicing.Positions.Count(p => p is GA.Business.Core.Fretboard.Primitives.Position.Played);
                            var passesNoteCountFilter = criteria.NoteCount switch
                            {
                                NoteCountFilter.TwoNotes => playedNotes == 2,
                                NoteCountFilter.ThreeNotes => playedNotes == 3,
                                NoteCountFilter.FourNotes => playedNotes == 4,
                                NoteCountFilter.FiveOrMore => playedNotes >= 5,
                                _ => true
                            };

                            if (!passesNoteCountFilter)
                            {
                                lock (progressLock)
                                {
                                    filteredCount++;
                                }
                                return;
                            }

                            // OPTIMIZATION 2: Inline decomposition (avoid creating intermediate list)
                            var relativeFrets = VoicingDecomposer.GetRelativeFrets(voicing.Positions);
                            if (relativeFrets == null)
                            {
                                lock (progressLock)
                                {
                                    filteredCount++;
                                }
                                return;
                            }

                            var index = variations.GetIndex(relativeFrets);
                            var matchingVector = vectorArray[(int)index];

                            // OPTIMIZATION 3: Only process prime forms (skip translations early)
                            if (matchingVector is not GA.Business.Core.Fretboard.Primitives.RelativeFretVector.PrimeForm primeForm)
                            {
                                lock (progressLock)
                                {
                                    filteredCount++;
                                }
                                return;
                            }

                            lock (progressLock)
                            {
                                decomposedCount++;
                            }

                            var decomposedVoicing = new DecomposedVoicing(voicing, matchingVector, primeForm, null);

                            // OPTIMIZATION 4: Analyze only after passing all cheap filters
                            var analysis = VoicingAnalyzer.AnalyzeEnhanced(decomposedVoicing);

                            // Apply remaining filters
                            if (!VoicingFilters.MatchesCriteria(voicing, analysis, criteria))
                            {
                                lock (progressLock)
                                {
                                    filteredCount++;
                                }
                                return;
                            }

                            var document = VoicingDocument.FromAnalysis(
                                voicing,
                                analysis,
                                primeForm.ToString(),
                                0);

                            lock (documentsLock)
                            {
                                // Check if we've reached max results
                                if (processedCount >= criteria.MaxResults)
                                {
                                    shouldStop = true;
                                    state.Stop();
                                    return;
                                }

                                _indexedDocuments.Add(document);
                                processedCount++;

                                if (processedCount >= criteria.MaxResults)
                                {
                                    _logger.LogInformation("Reached max results limit of {MaxResults}", criteria.MaxResults);
                                    shouldStop = true;
                                    state.Stop();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing voicing");
                            lock (progressLock)
                            {
                                errorCount++;
                            }
                        }
                    });
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Filtered indexing cancelled after processing {Count} voicings", processedCount);
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "Filtered indexing complete: {Count} documents indexed, {FilteredCount} filtered out, {DecomposedCount} decomposed in {ElapsedMs}ms",
                processedCount,
                filteredCount,
                decomposedCount,
                stopwatch.ElapsedMilliseconds);

            return new VoicingIndexingResult(
                Success: true,
                DocumentCount: processedCount,
                Duration: stopwatch.Elapsed,
                ErrorCount: errorCount,
                Message: $"Indexed {processedCount} voicings ({filteredCount} filtered out, {decomposedCount} decomposed, {errorCount} errors)");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to index filtered voicings");

            return new VoicingIndexingResult(
                Success: false,
                DocumentCount: 0,
                Duration: stopwatch.Elapsed,
                ErrorCount: 0,
                Message: $"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get documents by semantic tags
    /// </summary>
    public IEnumerable<VoicingDocument> GetByTags(params string[] tags)
    {
        return _indexedDocuments.Where(doc =>
            tags.All(tag => doc.SemanticTags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Get documents by difficulty
    /// </summary>
    public IEnumerable<VoicingDocument> GetByDifficulty(string difficulty)
    {
        return _indexedDocuments.Where(doc =>
            doc.Difficulty?.Equals(difficulty, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Get documents by position
    /// </summary>
    public IEnumerable<VoicingDocument> GetByPosition(string position)
    {
        return _indexedDocuments.Where(doc =>
            doc.Position?.Equals(position, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Get documents by chord name
    /// </summary>
    public IEnumerable<VoicingDocument> GetByChordName(string chordName)
    {
        return _indexedDocuments.Where(doc =>
            doc.ChordName?.Contains(chordName, StringComparison.OrdinalIgnoreCase) == true);
    }
}

/// <summary>
/// Result of voicing indexing operation
/// </summary>
public record VoicingIndexingResult(
    bool Success,
    int DocumentCount,
    TimeSpan Duration,
    int ErrorCount,
    string Message);

