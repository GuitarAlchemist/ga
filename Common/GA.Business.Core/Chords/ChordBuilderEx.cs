namespace GA.Business.Core.Chords;

using Notes;
using Tonal.Modes;

/// <summary>
/// Extension methods for ChordBuilder to provide additional functionality
/// </summary>
public static class ChordBuilderEx
{
    /// <summary>
    /// Creates a chord progression from a sequence of chord builders
    /// </summary>
    public static ChordProgression ToProgression(this IEnumerable<ChordBuilder> builders)
    {
        var chords = builders.Select(b => b.Build()).ToList();
        return new ChordProgression(chords);
    }

    /// <summary>
    /// Creates all diatonic triads from a scale mode
    /// </summary>
    public static IEnumerable<ChordBuilder> CreateDiatonicTriads(this ScaleMode mode)
    {
        for (int degree = 1; degree <= mode.Notes.Count; degree++)
        {
            yield return ChordBuilder.Create()
                .FromScaleMode(mode, degree, ChordExtension.Triad);
        }
    }

    /// <summary>
    /// Creates all diatonic seventh chords from a scale mode
    /// </summary>
    public static IEnumerable<ChordBuilder> CreateDiatonicSevenths(this ScaleMode mode)
    {
        for (int degree = 1; degree <= mode.Notes.Count; degree++)
        {
            yield return ChordBuilder.Create()
                .FromScaleMode(mode, degree, ChordExtension.Seventh);
        }
    }

    /// <summary>
    /// Creates a chord with quartal harmony (stacked fourths)
    /// </summary>
    public static ChordBuilder AsQuartalChord(this ChordBuilder builder, int numberOfFourths = 3)
    {
        builder.WithStackingType(ChordStackingType.Quartal);
        
        for (int i = 1; i < numberOfFourths; i++)
        {
            var semitones = i * 5; // Perfect fourth = 5 semitones
            builder.WithInterval(semitones, ChordFunction.Root, false);
        }
        
        return builder.WithName($"Quartal ({numberOfFourths} fourths)");
    }

    /// <summary>
    /// Creates a chord with quintal harmony (stacked fifths)
    /// </summary>
    public static ChordBuilder AsQuintalChord(this ChordBuilder builder, int numberOfFifths = 3)
    {
        builder.WithStackingType(ChordStackingType.Quintal);
        
        for (int i = 1; i < numberOfFifths; i++)
        {
            var semitones = i * 7; // Perfect fifth = 7 semitones
            builder.WithInterval(semitones, ChordFunction.Root, false);
        }
        
        return builder.WithName($"Quintal ({numberOfFifths} fifths)");
    }

    /// <summary>
    /// Creates a chord with secundal harmony (stacked seconds)
    /// </summary>
    public static ChordBuilder AsSecundalChord(this ChordBuilder builder, int numberOfSeconds = 4)
    {
        builder.WithStackingType(ChordStackingType.Secundal);
        
        for (int i = 1; i < numberOfSeconds; i++)
        {
            var semitones = i * 2; // Major second = 2 semitones
            builder.WithInterval(semitones, ChordFunction.Root, false);
        }
        
        return builder.WithName($"Secundal ({numberOfSeconds} seconds)");
    }

    /// <summary>
    /// Adds altered tensions to a dominant chord
    /// </summary>
    public static ChordBuilder WithAlteredTensions(this ChordBuilder builder)
    {
        return builder
            .WithFlatNinth()
            .WithSharpNinth()
            .WithSharpEleventh()
            .WithFlatThirteenth()
            .WithName("Altered Dominant");
    }

    /// <summary>
    /// Creates a polychord (chord over chord)
    /// </summary>
    public static ChordBuilder AsPolychord(this ChordBuilder upperChord, ChordBuilder lowerChord)
    {
        var upper = upperChord.Build();
        var lower = lowerChord.Build();
        
        var combinedBuilder = ChordBuilder.Create(lower.Root);
        
        // Add all intervals from lower chord
        foreach (var interval in lower.Formula.Intervals)
        {
            combinedBuilder.WithInterval(interval.Interval, interval.Function, interval.IsEssential);
        }
        
        // Add all intervals from upper chord, transposed
        var upperRootInterval = Interval.FromSemitones((upper.Root.MidiNote - lower.Root.MidiNote) % 12);
        foreach (var interval in upper.Formula.Intervals)
        {
            var combinedInterval = Interval.FromSemitones((upperRootInterval.Semitones + interval.Interval.Semitones) % 12);
            combinedBuilder.WithInterval(combinedInterval, ChordFunction.Root, false);
        }
        
        return combinedBuilder.WithName($"{upper.Symbol}/{lower.Symbol}");
    }

    /// <summary>
    /// Creates a slash chord (chord with different bass note)
    /// </summary>
    public static ChordBuilder WithBass(this ChordBuilder builder, Note bassNote)
    {
        var chord = builder.Build();
        var bassInterval = Interval.FromSemitones((bassNote.MidiNote - chord.Root.MidiNote) % 12);
        
        return ChordBuilder.Create(chord.Root)
            .WithName($"{chord.Symbol}/{bassNote.Name}")
            .WithInterval(bassInterval, ChordFunction.Root, true);
    }

    /// <summary>
    /// Creates a chord with specific voicing
    /// </summary>
    public static ChordBuilder WithVoicing(this ChordBuilder builder, params int[] voicingIntervals)
    {
        builder.WithName($"{builder.Build().Symbol} (custom voicing)");
        
        foreach (var interval in voicingIntervals)
        {
            builder.WithInterval(interval, ChordFunction.Root, false);
        }
        
        return builder;
    }
}

/// <summary>
/// Represents a chord progression
/// </summary>
public class ChordProgression
{
    /// <summary>
    /// Gets the chords in the progression
    /// </summary>
    public IReadOnlyList<Chord> Chords { get; }

    /// <summary>
    /// Initializes a new instance of the ChordProgression class
    /// </summary>
    public ChordProgression(IEnumerable<Chord> chords)
    {
        Chords = chords.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the Roman numeral analysis of the progression in the given key
    /// </summary>
    public string GetRomanNumeralAnalysis(Note key)
    {
        // This would require more complex harmonic analysis
        // For now, return a simple representation
        return string.Join(" - ", Chords.Select(c => c.Symbol));
    }

    /// <summary>
    /// Transposes the entire progression by the specified interval
    /// </summary>
    public ChordProgression Transpose(Interval interval)
    {
        var transposedChords = Chords.Select(chord =>
        {
            var newRoot = chord.Root + interval;
            return new Chord(newRoot, chord.Formula, chord.Symbol);
        });
        
        return new ChordProgression(transposedChords);
    }

    public override string ToString() => string.Join(" | ", Chords.Select(c => c.Symbol));
}
