namespace GA.Business.Core.Microservices.Examples;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
///     Example: Music service using F#-inspired monads
///     Demonstrates Option, Result, Reader, State, and Async monads
/// </summary>

#region Domain Models

public record Chord(string Name, string Quality, IReadOnlyList<int> PitchClasses);

public record Scale(string Name, IReadOnlyList<int> PitchClasses);

public record ChordProgression(string Name, IReadOnlyList<Chord> Chords);

#endregion

#region Service Dependencies (Reader Monad Environment)

public record MusicServiceDeps(
    IConfiguration Config,
    ILogger Logger,
    IMemoryCache Cache
);

#endregion

#region Service Implementation Using Monads

public class MonadicMusicService
{
    // Example 1: Option monad - handling nullable values
    public static Option<Chord> FindChordByName(string name, IReadOnlyList<Chord> chords)
    {
        return Option<Chord>.OfNullable(chords.FirstOrDefault(c => c.Name == name));
    }

    // Example 2: Result monad - validation with railway-oriented programming
    public static Result<Chord, string> ValidateChord(Chord chord)
    {
        if (string.IsNullOrWhiteSpace(chord.Name))
        {
            return new Result<Chord, string>.Failure("Chord name is required");
        }

        if (chord.PitchClasses.Count == 0)
        {
            return new Result<Chord, string>.Failure("Chord must have at least one pitch class");
        }

        if (chord.PitchClasses.Any(pc => pc < 0 || pc > 11))
        {
            return new Result<Chord, string>.Failure("Pitch classes must be between 0 and 11");
        }

        return new Result<Chord, string>.Success(chord);
    }

    // Example 3: Chaining Result operations (railway-oriented programming)
    public static Result<Chord, string> CreateAndValidateChord(string name, string quality,
        IReadOnlyList<int> pitchClasses)
    {
        var chord = new Chord(name, quality, pitchClasses);

        return ValidateChord(chord)
            .Bind(c => EnrichChord(c))
            .Tap(c => Console.WriteLine($"Created chord: {c.Name}"));
    }

    private static Result<Chord, string> EnrichChord(Chord chord)
    {
        // Add metadata, analyze intervals, etc.
        return new Result<Chord, string>.Success(chord);
    }

    // Example 4: Reader monad - dependency injection
    public static Reader<MusicServiceDeps, Option<Chord>> GetChordWithCache(string chordId)
    {
        return from deps in Reader.Ask<MusicServiceDeps>()
            let cacheKey = $"chord:{chordId}"
            let cached = Option<Chord>.OfNullable(deps.Cache.Get<Chord>(cacheKey))
            select cached.Match(
                chord =>
                {
                    deps.Logger.LogInformation($"Cache hit for chord {chordId}");
                    return cached;
                },
                () =>
                {
                    deps.Logger.LogInformation($"Cache miss for chord {chordId}");
                    // Load from database
                    var chord = LoadChordFromDatabase(chordId);
                    chord.Match<Unit>(
                        c =>
                        {
                            deps.Cache.Set(cacheKey, c, TimeSpan.FromMinutes(15));
                            return Unit.Value;
                        },
                        () => Unit.Value
                    );
                    return chord;
                }
            );
    }

    private static Option<Chord> LoadChordFromDatabase(string chordId)
    {
        // Simulate database load
        return new Option<Chord>.Some(new Chord("Cmaj7", "Major7", [0, 4, 7, 11]));
    }

    // Example 5: State monad - threading state through computations
    public static State<int, Chord> TransposeChord(Chord chord, int semitones)
    {
        return from currentTransposition in State<int, int>.Get
            let newTransposition = currentTransposition + semitones
            from _ in State<int, Unit>.Put(newTransposition)
            let transposedPitchClasses = chord.PitchClasses
                .Select(pc => (pc + semitones) % 12)
                .ToList()
            select new Chord(chord.Name, chord.Quality, transposedPitchClasses);
    }

    // Example 6: Async monad - asynchronous operations
    public static Async<Result<Chord, string>> LoadChordAsync(string chordId)
    {
        return from chord in Async.FromTask(LoadChordFromDatabaseAsync(chordId))
            select ValidateChord(chord);
    }

    private static async Task<Chord> LoadChordFromDatabaseAsync(string chordId)
    {
        await Task.Delay(100); // Simulate async database call
        return new Chord("Cmaj7", "Major7", [0, 4, 7, 11]);
    }

    // Example 7: Combining monads - Reader + Async + Result
    public static Reader<MusicServiceDeps, Async<Result<Chord, string>>> GetAndValidateChordAsync(string chordId)
    {
        return from deps in Reader.Ask<MusicServiceDeps>()
            select new Async<Result<Chord, string>>(async () =>
            {
                deps.Logger.LogInformation($"Loading chord {chordId}");

                var chord = await LoadChordFromDatabaseAsync(chordId);
                var validationResult = ValidateChord(chord);

                return validationResult.Match(
                    c =>
                    {
                        deps.Cache.Set($"chord:{chordId}", c, TimeSpan.FromMinutes(15));
                        return validationResult;
                    },
                    error =>
                    {
                        deps.Logger.LogWarning($"Chord validation failed: {error}");
                        return validationResult;
                    }
                );
            });
    }

    // Example 8: LINQ query syntax with monads
    public static Result<ChordProgression, string> CreateProgression(
        string name,
        IReadOnlyList<string> chordNames,
        IReadOnlyList<Chord> availableChords)
    {
        // Using LINQ query syntax with Result monad
        var chordsResult = chordNames
            .Select(name => FindChordByName(name, availableChords)
                .Match<Result<Chord, string>>(
                    chord => new Result<Chord, string>.Success(chord),
                    () => new Result<Chord, string>.Failure($"Chord not found: {name}")
                ))
            .ToList();

        // Check if all chords were found
        var failures = chordsResult.Where(r => r.IsFailure).ToList();
        if (failures.Any())
        {
            var errors = failures.Select(f => f.GetErrorOrThrow()).ToList();
            return new Result<ChordProgression, string>.Failure(string.Join(", ", errors));
        }

        var chords = chordsResult.Select(r => r.GetValueOrThrow()).ToList();
        return new Result<ChordProgression, string>.Success(new ChordProgression(name, chords));
    }

    // Example 9: Option monad with LINQ
    public static Option<Chord> FindFirstMajorChord(IReadOnlyList<Chord> chords)
    {
        return (from chord in chords
                where chord.Quality == "Major"
                select chord)
            .FirstOrDefault() is Chord c
                ? new Option<Chord>.Some(c)
                : new Option<Chord>.None();
    }

    // Example 10: Traverse - convert List<Option<T>> to Option<List<T>>
    public static Option<IReadOnlyList<Chord>> TraverseOptions(IReadOnlyList<Option<Chord>> options)
    {
        var chords = new List<Chord>();

        foreach (var option in options)
        {
            if (option is Option<Chord>.Some s)
            {
                chords.Add(s.Value);
            }
            else
            {
                return new Option<IReadOnlyList<Chord>>.None();
            }
        }

        return new Option<IReadOnlyList<Chord>>.Some(chords);
    }

    // Example 11: Sequence - convert List<Result<T, E>> to Result<List<T>, E>
    public static Result<IReadOnlyList<Chord>, string> SequenceResults(IReadOnlyList<Result<Chord, string>> results)
    {
        var chords = new List<Chord>();

        foreach (var result in results)
        {
            if (result is Result<Chord, string>.Success s)
            {
                chords.Add(s.Value);
            }
            else if (result is Result<Chord, string>.Failure f)
            {
                return new Result<IReadOnlyList<Chord>, string>.Failure(f.Error);
            }
        }

        return new Result<IReadOnlyList<Chord>, string>.Success(chords);
    }
}

#endregion

#region Usage Examples

public static class MonadicServiceUsageExamples
{
    public static void Example1_OptionMonad()
    {
        var chords = new List<Chord>
        {
            new("Cmaj7", "Major7", [0, 4, 7, 11]),
            new("Dm7", "Minor7", [2, 5, 9, 0])
        };

        var result = MonadicMusicService.FindChordByName("Cmaj7", chords)
            .Map(chord => $"Found: {chord.Name}")
            .GetOrElse("Chord not found");

        Console.WriteLine(result); // Output: Found: Cmaj7
    }

    public static void Example2_ResultMonad()
    {
        var validChord = new Chord("Cmaj7", "Major7", [0, 4, 7, 11]);
        var invalidChord = new Chord("", "Major7", [0, 4, 7, 11]);

        var result1 = MonadicMusicService.ValidateChord(validChord)
            .Match(
                c => $"Valid chord: {c.Name}",
                error => $"Invalid: {error}"
            );

        var result2 = MonadicMusicService.ValidateChord(invalidChord)
            .Match(
                c => $"Valid chord: {c.Name}",
                error => $"Invalid: {error}"
            );

        Console.WriteLine(result1); // Output: Valid chord: Cmaj7
        Console.WriteLine(result2); // Output: Invalid: Chord name is required
    }

    public static void Example3_ReaderMonad()
    {
        var deps = new MusicServiceDeps(
            null!,
            null!,
            new MemoryCache(new MemoryCacheOptions())
        );

        var chordOption = MonadicMusicService.GetChordWithCache("123").Run(deps);

        chordOption.Match<Unit>(
            chord =>
            {
                Console.WriteLine($"Got chord: {chord.Name}");
                return Unit.Value;
            },
            () =>
            {
                Console.WriteLine("Chord not found");
                return Unit.Value;
            }
        );
    }

    public static void Example4_StateMonad()
    {
        var chord = new Chord("C", "Major", [0, 4, 7]);
        var transposeOperation = MonadicMusicService.TransposeChord(chord, 2);

        var (transposedChord, finalState) = transposeOperation.Run(0);

        Console.WriteLine($"Transposed to: {string.Join(", ", transposedChord.PitchClasses)}");
        Console.WriteLine($"Total transposition: {finalState} semitones");
    }

    public static async Task Example5_AsyncMonad()
    {
        var asyncResult = MonadicMusicService.LoadChordAsync("123");
        var result = await asyncResult.ToTask();

        result.Match<Unit>(
            chord =>
            {
                Console.WriteLine($"Loaded: {chord.Name}");
                return Unit.Value;
            },
            error =>
            {
                Console.WriteLine($"Error: {error}");
                return Unit.Value;
            }
        );
    }
}

#endregion
