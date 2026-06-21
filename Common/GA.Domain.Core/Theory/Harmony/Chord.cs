namespace GA.Domain.Core.Theory.Harmony;

using System.Text.RegularExpressions;
using Atonal;
using Design.Attributes;
using Design.Schema;
using Extensions;
using Primitives.Intervals;
using Primitives.Notes;
using Interval = Primitives.Intervals.Interval;

/// <summary>
///     Represents a musical chord with its notes, intervals, and harmonic properties
///     (<see href="https://en.wikipedia.org/wiki/Chord_(music)" />)
/// </summary>
[PublicAPI]
[DomainInvariant("A chord must have a root note and a pitch class set", "Root != null && PitchClassSet != null")]
[DomainRelationship(typeof(PitchClassSet), RelationshipType.IsChildOf, "A chord is a tonal realization of a pitch class set")]
// @ai:business-value foundational domain model — every Voicing, ChordRecognizer, and chatbot answer constructs or reasons about Chord instances [T:manually-reviewed conf:0.9 src:product-owner@2026-05-24]
public sealed class Chord : IEquatable<Chord>
{
    /// <summary>
    ///     Initializes a new instance of the Chord class
    /// </summary>
    public Chord(Note root, ChordFormula formula, string? symbol = null)
    {
        Root = root;
        Formula = formula;

        // Build notes from formula
        List<Note.Accidented> notes = [root.ToAccidented()];
        foreach (var interval in formula.Intervals)
        {
            // Transpose by adding semitones to pitch class value
            var newPitchClassValue = (root.PitchClass.Value + interval.Interval.Semitones.Value) % 12;
            var newNote = new PitchClass { Value = newPitchClassValue }.ToChromaticNote().ToAccidented();
            notes.Add(newNote);
        }

        Notes = new(notes);
        PitchClassSet = Notes.ToPitchClassSet();

        Symbol = symbol ?? GenerateSymbol();
    }

    /// <summary>
    ///     Initializes a new instance of the Chord class from notes
    /// </summary>
    public Chord(AccidentedNoteCollection notes, Note? root = null)
    {
        if (notes.Count < 2)
        {
            throw new ArgumentException("A chord must have at least 2 notes", nameof(notes));
        }

        Notes = notes;
        Root = root ?? notes[0];
        PitchClassSet = notes.ToPitchClassSet();

        // Analyze the chord to determine formula
        Formula = AnalyzeChordFormula();
        Symbol = GenerateSymbol();
    }

    // Splits a chord symbol into root (A-G with optional #/b) and a suffix describing quality/extension.
    private static readonly Regex _symbolRegex =
        new("^([A-G][#b]?)(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    ///     Parses a chord symbol (e.g. "C", "Cm", "Cmaj7", "F#m7b5") into a <see cref="Chord" />.
    /// </summary>
    /// <param name="symbol">The chord symbol. The root is A-G with an optional # or b; the remainder is the quality/extension suffix.</param>
    /// <returns>The parsed <see cref="Chord" />.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="symbol" /> is null/blank or not a recognizable chord symbol.</exception>
    public static Chord FromSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Chord symbol cannot be null or empty", nameof(symbol));
        }

        var match = _symbolRegex.Match(symbol.Trim());
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid chord symbol: {symbol}", nameof(symbol));
        }

        var root = Note.Accidented.Parse(match.Groups[1].Value, null);
        var formula = ParseSuffix(match.Groups[2].Value);
        return new(root, formula, symbol);
    }

    /// <summary>
    ///     Attempts to parse a chord symbol into a <see cref="Chord" /> without throwing.
    /// </summary>
    public static bool TryFromSymbol(string symbol, out Chord? chord)
    {
        try
        {
            chord = FromSymbol(symbol);
            return true;
        }
        catch (ArgumentException)
        {
            chord = null;
            return false;
        }
    }

    private static ChordFormula ParseSuffix(string suffix)
    {
        var s = suffix.Trim().ToLowerInvariant().Replace(" ", "");
        return s switch
        {
            "" or "maj" or "major" => ChordFormula.Major,
            "m" or "min" or "minor" or "-" => ChordFormula.Minor,
            "dim" or "°" => ChordFormula.Diminished,
            "aug" or "+" => ChordFormula.Augmented,
            "sus2" => ChordFormula.FromSemitones("Sus2", 2, 7),
            "sus" or "sus4" => ChordFormula.FromSemitones("Sus4", 5, 7),
            "6" => ChordFormula.FromSemitones("Sixth", 4, 7, 9),
            "m6" or "min6" => ChordFormula.FromSemitones("Minor Sixth", 3, 7, 9),
            "7" => ChordFormula.Dominant7,
            "maj7" or "△7" => ChordFormula.Major7,
            "m7" or "min7" or "-7" => ChordFormula.Minor7,
            "dim7" or "°7" => ChordFormula.FromSemitones("Diminished 7th", 3, 6, 9),
            "m7b5" or "ø7" => ChordFormula.FromSemitones("Half Diminished 7th", 3, 6, 10),
            "9" => ChordFormula.FromSemitones("Dominant 9th", 4, 7, 10, 14),
            "maj9" or "△9" => ChordFormula.FromSemitones("Major 9th", 4, 7, 11, 14),
            "m9" or "min9" or "-9" => ChordFormula.FromSemitones("Minor 9th", 3, 7, 10, 14),
            "11" => ChordFormula.FromSemitones("Dominant 11th", 4, 7, 10, 14, 17),
            "maj11" or "△11" => ChordFormula.FromSemitones("Major 11th", 4, 7, 11, 14, 17),
            "m11" or "min11" or "-11" => ChordFormula.FromSemitones("Minor 11th", 3, 7, 10, 14, 17),
            "13" => ChordFormula.FromSemitones("Dominant 13th", 4, 7, 10, 14, 17, 21),
            "maj13" or "△13" => ChordFormula.FromSemitones("Major 13th", 4, 7, 11, 14, 17, 21),
            "m13" or "min13" or "-13" => ChordFormula.FromSemitones("Minor 13th", 3, 7, 10, 14, 17, 21),
            "add9" => ChordFormula.FromSemitones("Add9", 4, 7, 14),
            "6/9" or "69" => ChordFormula.FromSemitones("6/9", 4, 7, 9, 14),
            _ => throw new ArgumentException($"Unrecognized chord suffix: '{suffix}'", nameof(suffix))
        };
    }

    /// <summary>
    ///     Gets the root note of the chord
    /// </summary>
    public Note Root { get; }

    /// <summary>
    ///     Gets the collection of notes in the chord
    /// </summary>
    public AccidentedNoteCollection Notes { get; }

    /// <summary>
    ///     Gets the chord formula (intervals from root)
    /// </summary>
    public ChordFormula Formula { get; }

    /// <summary>
    ///     Gets the chord symbol (e.g., "Cmaj7", "Am", "F#dim")
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    ///     Gets the chord quality (major, minor, dominant, suspended, etc.).
    ///     Delegates to <see cref="Formula" /> so a chord and its formula never disagree — e.g. a
    ///     dominant-7th chord reports Dominant (not Major) and a sus chord reports Suspended.
    /// </summary>
    public ChordQuality Quality => Formula.Quality;

    /// <summary>
    ///     Gets the chord extension (7th, 9th, 11th, 13th, sus, 6, …). Delegates to <see cref="Formula" />.
    /// </summary>
    public ChordExtension Extension => Formula.Extension;

    /// <summary>
    ///     Gets the pitch class set representation of the chord
    /// </summary>
    public PitchClassSet PitchClassSet { get; }

    /// <summary>
    ///     Gets whether this is an inverted chord
    /// </summary>
    public bool IsInverted => Notes[0] != Root;

    /// <summary>
    ///     Gets the bass note (lowest note in the voicing)
    /// </summary>
    public Note Bass => Notes[0];

    public bool Equals(Chord? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return PitchClassSet.Equals(other.PitchClassSet) && Root.Equals(other.Root);
    }


    /// <summary>
    ///     Gets the inversion of this chord (0 = root position, 1 = first inversion, etc.)
    /// </summary>
    public int GetInversion()
    {
        if (!IsInverted)
        {
            return 0;
        }

        var rootIndex = Notes.ToList().FindIndex(n => n.PitchClass == Root.PitchClass);
        if (rootIndex == -1)
        {
            return 0;
        }

        return (Notes.Count - rootIndex) % Notes.Count;
    }

    /// <summary>
    ///     Creates a new chord in the specified inversion
    /// </summary>
    public Chord ToInversion(int inversion)
    {
        if (inversion < 0 || inversion >= Notes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(inversion));
        }

        if (inversion == 0)
        {
            return this;
        }

        var notesList = Notes.ToList();
        var invertedNotes = notesList.Skip(inversion).Concat(notesList.Take(inversion));

        return new(new(invertedNotes.ToList()), Root);
    }

    private ChordFormula AnalyzeChordFormula()
    {
        List<ChordFormulaInterval> intervals = [];

        foreach (var note in Notes.Skip(1)) // Skip root
        {
            var semitones = (note.PitchClass.Value - Root.PitchClass.Value + 12) % 12;
            var interval = new Interval.Chromatic(Semitones.FromValue(semitones));

            var function = ChordFunctionExtensions.FromSemitones(semitones);
            intervals.Add(new(interval, function));
        }

        return new($"Analyzed_{Root}", intervals);
    }

    private string GenerateSymbol()
    {
        var symbol = "";
        if (Root is Note.Accidented accidented)
        {
            symbol = accidented.NaturalNote.ToString();
            if (accidented.Accidental != Accidental.Natural)
            {
                symbol += accidented.Accidental?.ToString() ?? "";
            }
        }
        else
        {
            symbol = Root.ToString();
        }

        symbol += Quality switch
        {
            ChordQuality.Minor => "m",
            ChordQuality.Diminished => "dim",
            ChordQuality.Augmented => "aug",
            _ => ""
        };

        symbol += Extension switch
        {
            ChordExtension.Seventh => "7",
            ChordExtension.Ninth => "9",
            ChordExtension.Eleventh => "11",
            ChordExtension.Thirteenth => "13",
            ChordExtension.Add9 => "add9",
            ChordExtension.Sixth => "6",
            ChordExtension.Sus2 => "sus2",
            ChordExtension.Sus4 => "sus4",
            _ => ""
        };

        return symbol;
    }

    public override bool Equals(object? obj) => Equals(obj as Chord);

    public override int GetHashCode() => HashCode.Combine(PitchClassSet, Root);

    public override string ToString() => Symbol;
}
