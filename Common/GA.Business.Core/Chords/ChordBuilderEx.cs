namespace GA.Business.Core.Chords;

using System.Collections.Generic;
using System.Linq;
using Atonal;
using Intervals;
using Intervals.Primitives;
using Notes;
using Tonal.Modes;

/// <summary>
///     Extension methods for ChordBuilder to provide additional functionality
/// </summary>
public static class ChordBuilderEx
{
    /// <summary>
    ///     Creates a chord progression from a sequence of chord builders
    /// </summary>
    public static ChordProgression ToProgression(this IEnumerable<ChordBuilder> builders)
    {
        var chords = builders.Select(b => b.Build()).ToList();
        return new(chords);
    }

    /// <summary>
    ///     Creates all diatonic triads from a scale mode
    /// </summary>
    public static IEnumerable<ChordBuilder> CreateDiatonicTriads(this ScaleMode mode)
    {
        for (var degree = 1; degree <= mode.Notes.Count; degree++)
        {
            yield return ChordBuilder.Create()
                .FromScaleMode(mode, degree);
        }
    }

    /// <summary>
    ///     Creates all diatonic seventh chords from a scale mode
    /// </summary>
    public static IEnumerable<ChordBuilder> CreateDiatonicSevenths(this ScaleMode mode)
    {
        for (var degree = 1; degree <= mode.Notes.Count; degree++)
        {
            yield return ChordBuilder.Create()
                .FromScaleMode(mode, degree, ChordExtension.Seventh);
        }
    }

    /// <summary>
    ///     Creates a chord with quartal harmony (stacked fourths)
    /// </summary>
    public static ChordBuilder AsQuartalChord(this ChordBuilder builder, int numberOfFourths = 3)
    {
        builder.WithStackingType(ChordStackingType.Quartal);

        for (var i = 1; i < numberOfFourths; i++)
        {
            var semitones = i * 5; // Perfect fourth = 5 semitones
            builder.WithInterval(semitones, ChordFunction.Root, false);
        }

        return builder.WithName($"Quartal ({numberOfFourths} fourths)");
    }

    /// <summary>
    ///     Creates a chord with quintal harmony (stacked fifths)
    /// </summary>
    public static ChordBuilder AsQuintalChord(this ChordBuilder builder, int numberOfFifths = 3)
    {
        builder.WithStackingType(ChordStackingType.Quintal);

        for (var i = 1; i < numberOfFifths; i++)
        {
            var semitones = i * 7; // Perfect fifth = 7 semitones
            builder.WithInterval(semitones, ChordFunction.Root, false);
        }

        return builder.WithName($"Quintal ({numberOfFifths} fifths)");
    }

    /// <summary>
    ///     Creates a chord with secundal harmony (stacked seconds)
    /// </summary>
    public static ChordBuilder AsSecundalChord(this ChordBuilder builder, int numberOfSeconds = 4)
    {
        builder.WithStackingType(ChordStackingType.Secundal);

        for (var i = 1; i < numberOfSeconds; i++)
        {
            var semitones = i * 2; // Major second = 2 semitones
            builder.WithInterval(semitones, ChordFunction.Root, false);
        }

        return builder.WithName($"Secundal ({numberOfSeconds} seconds)");
    }

    /// <summary>
    ///     Adds altered tensions to a dominant chord
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
    ///     Creates a polychord (chord over chord)
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
        var upperRootSemitones = (upper.Root.PitchClass.Value - lower.Root.PitchClass.Value + 12) % 12;
        var upperRootInterval = new Interval.Chromatic(Semitones.FromValue(upperRootSemitones));
        foreach (var interval in upper.Formula.Intervals)
        {
            var combinedSemitones = (upperRootInterval.Semitones.Value + interval.Interval.Semitones.Value) % 12;
            var combinedInterval = new Interval.Chromatic(Semitones.FromValue(combinedSemitones));
            combinedBuilder.WithInterval((Interval)combinedInterval, ChordFunction.Root, false);
        }

        return combinedBuilder.WithName($"{upper.Symbol}/{lower.Symbol}");
    }

    /// <summary>
    ///     Creates a slash chord (chord with different bass note)
    /// </summary>
    public static ChordBuilder WithBass(this ChordBuilder builder, Note bassNote)
    {
        var chord = builder.Build();
        var bassSemitones = (bassNote.PitchClass.Value - chord.Root.PitchClass.Value + 12) % 12;
        var bassInterval = new Interval.Chromatic(Semitones.FromValue(bassSemitones));

        return ChordBuilder.Create(chord.Root)
            .WithName($"{chord.Symbol}/{bassNote}")
            .WithInterval((Interval)bassInterval, ChordFunction.Root);
    }

    /// <summary>
    ///     Creates a chord with specific voicing
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
///     Represents a chord progression
/// </summary>
public class ChordProgression
{
    /// <summary>
    ///     Initializes a new instance of the ChordProgression class
    /// </summary>
    public ChordProgression(IEnumerable<Chord> chords)
    {
        Chords = chords.ToList().AsReadOnly();
    }

    /// <summary>
    ///     Gets the chords in the progression
    /// </summary>
    public IReadOnlyList<Chord> Chords { get; }

    /// <summary>
    ///     Gets the Roman numeral analysis of the progression in the given key
    /// </summary>
    public string GetRomanNumeralAnalysis(Note key)
    {
        // This would require more complex harmonic analysis
        // For now, return a simple representation
        return string.Join(" - ", Chords.Select(c => c.Symbol));
    }

    /// <summary>
    ///     Transposes the entire progression by the specified interval
    /// </summary>
    public ChordProgression Transpose(Interval interval)
    {
        var transposedChords = Chords.Select(chord =>
        {
            var newPitchClassValue = (chord.Root.PitchClass.Value + interval.Semitones.Value) % 12;
            var newRoot = new PitchClass { Value = newPitchClassValue }.ToChromaticNote().ToAccidented();
            return new Chord(newRoot, chord.Formula, chord.Symbol);
        });

        return new(transposedChords);
    }

    public override string ToString()
    {
        return string.Join(" | ", Chords.Select(c => c.Symbol));
    }
}
