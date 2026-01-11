namespace GA.Business.Core.Fretboard.Voicings.Generation;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Core;
using Positions;
using Primitives;
using GA.Business.Core.Notes.Primitives;

/// <summary>
/// Generates all possible voicings on a fretboard within specified constraints
/// </summary>
public static class VoicingGenerator
{
    /// <summary>
    /// Generates all possible voicings within a fret window using cached instances for optimal performance
    /// </summary>
    public static List<Voicing> GenerateAllVoicingsInWindowOptimized(
        Fretboard fretboard,
        int startFret,
        int endFret,
        Fret[] cachedFrets,
        Str[] cachedStrings,
        Position.Muted[] cachedMutedPositions,
        PositionLocation[,] cachedLocations,
        int minPlayedNotes = 2,
        int maxFretSpan = 4)
    {
        var stringCount = fretboard.StringCount;

        // Build position options for each string
        var positionsPerString = new List<Position>[stringCount];
        for (var stringIndex = 0; stringIndex < stringCount; stringIndex++)
        {
            var str = cachedStrings[stringIndex];

            // Use cached Position.Muted
            var positions = new List<Position> { cachedMutedPositions[stringIndex] };

            // Add frets within window
            for (var fret = startFret; fret <= endFret; fret++)
            {
                // Use cached PositionLocation
                var location = cachedLocations[stringIndex, fret];
                var openStringPitch = fretboard.Tuning[str];
                var midiNote = openStringPitch.MidiNote + fret;
                positions.Add(new Position.Played(location, midiNote));
            }

            positionsPerString[stringIndex] = positions;
        }

        // Calculate total combinations and pre-allocate results list
        var totalCombinations = 1;
        for (var i = 0; i < stringCount; i++)
        {
            totalCombinations *= positionsPerString[i].Count;
        }

        var results = new List<Voicing>(totalCombinations);

        // Pre-calculate counts array
        var positionsPerStringCounts = new int[stringCount];
        for (var i = 0; i < stringCount; i++)
        {
            positionsPerStringCounts[i] = positionsPerString[i].Count;
        }

        // Use stackalloc for indices array (Rust-inspired: stack allocation)
        Span<int> indices = stackalloc int[stringCount];

        for (var i = 0; i < totalCombinations; i++)
        {
            // Build combination from current indices
            var combination = new Position[stringCount];
            for (var s = 0; s < stringCount; s++)
            {
                combination[s] = positionsPerString[s][indices[s]];
            }

            // Filter: Must have at least minPlayedNotes played notes
            var playedCount = 0;
            var minFret = int.MaxValue;
            var maxFret = int.MinValue;

            // Single pass to count played notes and find min/max frets
            for (var s = 0; s < stringCount; s++)
            {
                if (combination[s] is Position.Played played)
                {
                    playedCount++;
                    var fretValue = played.Location.Fret.Value;
                    if (fretValue > 0) // Exclude open strings from span calculation
                    {
                        if (fretValue < minFret) minFret = fretValue;
                        if (fretValue > maxFret) maxFret = fretValue;
                    }
                }
            }

            if (playedCount >= minPlayedNotes)
            {
                // Check fret span constraint
                var isValid = minFret == int.MaxValue || maxFret - minFret <= maxFretSpan;

                if (isValid)
                {
                    // Extract MIDI notes
                    var notes = new MidiNote[playedCount];
                    var noteIndex = 0;
                    for (var s = 0; s < stringCount; s++)
                    {
                        if (combination[s] is Position.Played played)
                        {
                            notes[noteIndex++] = played.MidiNote;
                        }
                    }

                    results.Add(new(combination, notes));
                }
            }

            // Increment indices (odometer pattern)
            for (var s = 0; s < stringCount; s++)
            {
                indices[s]++;
                if (indices[s] < positionsPerStringCounts[s])
                    break;
                indices[s] = 0;
            }
        }

        return results;
    }

    /// <summary>
    /// Generates all possible voicings across the entire fretboard using a sliding window approach with channels
    /// and returns them as an async enumerable stream
    /// </summary>
    /// <param name="fretboard">The fretboard to generate voicings on</param>
    /// <param name="windowSize">Size of the sliding window in frets (default: 4 for 5-fret span)</param>
    /// <param name="minPlayedNotes">Minimum number of played notes (default: 2)</param>
    /// <param name="parallel">Whether to use parallel processing (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable stream of unique voicings</returns>
    public static async IAsyncEnumerable<Voicing> GenerateAllVoicingsAsync(
        Fretboard fretboard,
        int windowSize = 4,
        int minPlayedNotes = 2,
        bool parallel = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var maxStartFret = fretboard.FretCount - windowSize;

        // Pre-cache instances once for all windows
        var cachedFrets = Fret.ItemsSpan.ToArray();
        var cachedStrings = Str.Range(fretboard.StringCount).ToArray();
        var cachedMutedPositions = cachedStrings.Select(s => new Position.Muted(s)).ToArray();

        var cachedLocations = new PositionLocation[fretboard.StringCount, fretboard.FretCount + 1];
        var fretMin = Fret.Min.Value;
        for (var s = 0; s < fretboard.StringCount; s++)
        {
            for (var f = 0; f <= fretboard.FretCount; f++)
            {
                cachedLocations[s, f] = new(cachedStrings[s], cachedFrets[f - fretMin]);
            }
        }

        if (parallel)
        {
            // Use channels for parallel processing with ordering preserved
            var channel = Channel.CreateUnbounded<(int WindowIndex, List<Voicing> Voicings)>(new()
            {
                SingleReader = true,
                SingleWriter = false
            });

            // Producer: Generate voicings for each window in parallel
            var producerTask = Task.Run(async () =>
            {
                await Parallel.ForEachAsync(
                    Enumerable.Range(0, maxStartFret + 1),
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = cancellationToken
                    },
                    async (startFret, ct) =>
                    {
                        var endFret = startFret + windowSize;
                        var voicings = GenerateAllVoicingsInWindowOptimized(
                            fretboard,
                            startFret,
                            endFret,
                            cachedFrets,
                            cachedStrings,
                            cachedMutedPositions,
                            cachedLocations,
                            minPlayedNotes,
                            maxFretSpan: windowSize);

                        await channel.Writer.WriteAsync((startFret, voicings), ct);
                    });

                channel.Writer.Complete();
            }, cancellationToken);

            // Consumer: Process results as they come in
            // For true streaming we can't guarantee global order perfectly without buffering,
            // but we can yield window-by-window if we want to stream out faster.
            // However, to maintain the original contract of deduplication across windows,
            // we really should just lock the HashSet or accept that dedupe might drift if we don't strictly order windows.
            // But since windows overlap, strict ordering is better for the seenDiagrams logic.
            
            // To fix "stalled" UI, we will process windows as they complete, but we must be careful with duplicate detection.
            // The safest parallel way is to collect all, sort, then dedupe. 
            // BUT that blocks the UI until ALL valid voicings are generated (millions).
            
            // ALTERNATIVE: Use a concurrent dictionary for seen diagrams and yield immediately.
            // We lose strict fret-order, but indexing doesn't care about order.
            
            var seenDiagrams = new ConcurrentDictionary<string, byte>();

            // Just stream results as they are ready
            await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
            {
                foreach (var voicing in result.Voicings)
                {
                    var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
                    if (seenDiagrams.TryAdd(diagram, 0))
                    {
                        yield return voicing;
                    }
                }
            }

            await producerTask;
        }
        else
        {
            // Sequential processing
            var seenDiagrams = new HashSet<string>();

            for (var startFret = 0; startFret <= maxStartFret; startFret++)
            {
                var endFret = startFret + windowSize;
                var voicings = GenerateAllVoicingsInWindowOptimized(
                    fretboard,
                    startFret,
                    endFret,
                    cachedFrets,
                    cachedStrings,
                    cachedMutedPositions,
                    cachedLocations,
                    minPlayedNotes,
                    maxFretSpan: windowSize);

                foreach (var voicing in voicings)
                {
                    var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
                    if (seenDiagrams.Add(diagram))
                    {
                        yield return voicing;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Collects all voicings into a list (convenience method)
    /// </summary>
    public static async Task<List<Voicing>> ToListAsync(
        this IAsyncEnumerable<Voicing> source,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Voicing>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            results.Add(item);
        }
        return results;
    }

    /// <summary>
    /// Synchronous wrapper that collects all voicings into a list
    /// </summary>
    public static List<Voicing> GenerateAllVoicings(
        Fretboard fretboard,
        int windowSize = 4,
        int minPlayedNotes = 2,
        bool parallel = true)
    {
        return GenerateAllVoicingsAsync(fretboard, windowSize, minPlayedNotes, parallel)
            .ToListAsync()
            .GetAwaiter()
            .GetResult();
    }
}

