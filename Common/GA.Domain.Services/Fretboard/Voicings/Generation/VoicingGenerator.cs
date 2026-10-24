namespace GA.Domain.Services.Fretboard.Voicings.Generation;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Domain.Core.Instruments.Positions;
using Domain.Core.Instruments.Primitives;
using Domain.Core.Primitives.Notes;

/// <summary>
///     Generates all possible voicings on a fretboard within specified constraints
/// </summary>
public static class VoicingGenerator
{
    /// <summary>
    ///     Generates all possible voicings within a fret window using cached instances for optimal performance
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
                        if (fretValue < minFret)
                        {
                            minFret = fretValue;
                        }

                        if (fretValue > maxFret)
                        {
                            maxFret = fretValue;
                        }
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
                {
                    break;
                }

                indices[s] = 0;
            }
        }

        return results;
    }

    /// <summary>
    ///     Generates all possible voicings across the entire fretboard using a sliding window approach with channels
    ///     and returns them as an async enumerable stream
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
            // Windows are generated in parallel, and each producer also builds its voicings' diagrams and
            // drops the duplicates inside its window, which is most of the work. The consumer then emits
            // the windows in fret order, so the stream is exactly the sequential one: the same voicings in
            // the same order on every run. It used to follow thread scheduling, and doing the diagrams on
            // the single consumer made the parallel path slower than the sequential one.
            var channel = Channel.CreateUnbounded<(int WindowIndex, List<Voicing> Voicings)>(new()
            {
                SingleReader = true,
                SingleWriter = false
            });

            // Cancelled when the consumer stops early, so the producers do not generate every window
            using var producerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var producerTask = Task.Run(async () =>
            {
                Exception? completionError = null;
                try
                {
                    await Parallel.ForEachAsync(
                        Enumerable.Range(0, maxStartFret + 1),
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount,
                            CancellationToken = producerCancellation.Token
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
                                windowSize);

                            var seenInWindow = new HashSet<string>(voicings.Count);
                            var unique = new List<Voicing>(voicings.Count);
                            foreach (var voicing in voicings)
                            {
                                if (seenInWindow.Add(voicing.Diagram))
                                {
                                    unique.Add(voicing);
                                }
                            }

                            await channel.Writer.WriteAsync((startFret, unique), ct);
                        });
                }
                catch (Exception ex)
                {
                    completionError = ex;
                }
                finally
                {
                    channel.Writer.TryComplete(completionError);
                }
            });

            try
            {
                var seenDiagrams = new HashSet<string>();
                var pending = new Dictionary<int, List<Voicing>>();
                var nextWindow = 0;

                await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    pending.Add(result.WindowIndex, result.Voicings);
                    while (pending.Remove(nextWindow, out var window))
                    {
                        nextWindow++;
                        foreach (var voicing in window)
                        {
                            if (seenDiagrams.Add(voicing.Diagram))
                            {
                                yield return voicing;
                            }
                        }
                    }
                }
            }
            finally
            {
                await producerCancellation.CancelAsync();
                await producerTask;
            }
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
                    windowSize);

                foreach (var voicing in voicings)
                {
                    var diagram = voicing.Diagram;
                    if (seenDiagrams.Add(diagram))
                    {
                        yield return voicing;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Synchronous wrapper that collects all voicings into a list
    /// </summary>
    public static List<Voicing> GenerateAllVoicings(
        Fretboard fretboard,
        int windowSize = 4,
        int minPlayedNotes = 2,
        bool parallel = true) =>
        GenerateAllVoicingsAsync(fretboard, windowSize, minPlayedNotes, parallel)
            .ToListAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>
    ///     Collects all voicings into a list (convenience method)
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
}
