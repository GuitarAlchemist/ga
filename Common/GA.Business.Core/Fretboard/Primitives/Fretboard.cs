namespace GA.Business.Core.Fretboard.Primitives;

using System;
using System.Collections.Generic;
using Atonal;
using Notes;
using Positions;

/// <summary>
///     Represents a fretboard with a specific tuning and number of frets
/// </summary>
public sealed class Fretboard
{
    /// <summary>
    ///     Initializes a new fretboard with the specified tuning and fret count
    /// </summary>
    public Fretboard(Tuning tuning, int fretCount)
    {
        Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        FretCount = fretCount;
    }

    /// <summary>
    ///     The tuning of the instrument
    /// </summary>
    public Tuning Tuning { get; }

    /// <summary>
    ///     Number of frets on the instrument
    /// </summary>
    public int FretCount { get; }

    /// <summary>
    ///     Number of strings on the instrument
    /// </summary>
    public int StringCount => Tuning.StringCount;

    /// <summary>
    ///     Gets the default standard guitar fretboard (6 strings, 24 frets, standard tuning)
    /// </summary>
    public static Fretboard Default => CreateStandardGuitar();

    /// <summary>
    ///     Gets the note at the specified string and fret
    /// </summary>
    /// <param name="stringIndex">Zero-based string index (0 = lowest string)</param>
    /// <param name="fret">Fret number (0 = open string)</param>
    /// <returns>The note at the specified position</returns>
    public Note GetNote(int stringIndex, int fret)
    {
        if (stringIndex < 0 || stringIndex >= StringCount)
        {
            throw new ArgumentOutOfRangeException(nameof(stringIndex));
        }

        if (fret < 0 || fret > FretCount)
        {
            throw new ArgumentOutOfRangeException(nameof(fret));
        }

        // Get the open string pitch and add the fret offset
        var openStringPitch = Tuning[new(stringIndex + 1)]; // Tuning uses 1-based string indexing
        var resultMidiNote = openStringPitch.MidiNote + fret; // Add semitones for fret position
        var resultPitch = Pitch.Chromatic.FromPitch(openStringPitch).Note.PitchClass
            .ToChromaticPitch(resultMidiNote.Octave);

        return resultPitch.Note;
    }

    /// <summary>
    ///     Gets all possible positions for a specific note
    /// </summary>
    public IEnumerable<Position> GetPositionsForNote(Note note)
    {
        for (var stringIndex = 0; stringIndex < StringCount; stringIndex++)
        {
            for (var fret = 0; fret <= FretCount; fret++)
            {
                if (GetNote(stringIndex, fret).Equals(note))
                {
                    var location = new PositionLocation(new(stringIndex + 1), new(fret));
                    var openStringPitch = Tuning[new(stringIndex + 1)];
                    var midiNote = openStringPitch.MidiNote + fret;
                    yield return new Position.Played(location, midiNote);
                }
            }
        }
    }

    /// <summary>
    ///     Checks if a position is valid on this fretboard
    /// </summary>
    public bool IsValidPosition(Position position)
    {
        return position.Location.Str.Value >= 1 &&
               position.Location.Str.Value <= StringCount &&
               position.Location.Fret.Value >= 0 &&
               position.Location.Fret.Value <= FretCount;
    }

    /// <summary>
    ///     Gets the pitch class of the note at the specified position
    /// </summary>
    public PitchClass GetPitchClass(int stringIndex, int fret)
    {
        return GetNote(stringIndex, fret).PitchClass;
    }

    /// <summary>
    ///     Creates a standard guitar fretboard (6 strings, 24 frets, standard tuning)
    /// </summary>
    public static Fretboard CreateStandardGuitar()
    {
        return new(Tuning.Default, 24);
    }

    /// <summary>
    ///     Creates a fretboard with the specified number of frets using standard guitar tuning
    /// </summary>
    public static Fretboard CreateGuitar(int fretCount)
    {
        return new(Tuning.Default, fretCount);
    }

    public override string ToString()
    {
        return $"Fretboard: {StringCount} strings, {FretCount} frets, Tuning: {Tuning}";
    }
}
