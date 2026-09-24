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

        // Build notes from formula, each spelled with the letter of its chord degree
        var accidentedRoot = root.ToAccidented();
        var semitonesFromRoot = formula.Intervals.Select(i => i.Interval.Semitones.Value % 12).ToHashSet();
        List<Note.Accidented> notes = [accidentedRoot];
        foreach (var interval in formula.Intervals)
        {
            notes.Add(SpellChordTone(accidentedRoot, interval.Interval.Semitones.Value, semitonesFromRoot));
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

    private Chord(Chord source, AccidentedNoteCollection notes)
    {
        Root = source.Root;
        Formula = source.Formula;
        Symbol = source.Symbol;
        PitchClassSet = source.PitchClassSet;
        Notes = notes;
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

        if (!TryFromSymbol(symbol, out var chord) || chord is null)
        {
            throw new ArgumentException($"Invalid chord symbol: {symbol}", nameof(symbol));
        }

        return chord;
    }

    /// <summary>
    ///     Attempts to parse a chord symbol into a <see cref="Chord" /> without throwing.
    /// </summary>
    public static bool TryFromSymbol(string symbol, out Chord? chord)
    {
        chord = null;
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        var match = _symbolRegex.Match(symbol.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!Note.Accidented.TryParse(match.Groups[1].Value, null, out var root))
        {
            return false;
        }

        if (!TryParseSuffix(match.Groups[2].Value, out var formula) || formula is null)
        {
            return false;
        }

        chord = new(root, formula, symbol);
        return true;
    }

    /// <summary>
    ///     Spells a chord tone with the letter of its chord degree (root letter + 2 letters per third,
    ///     so the third of C minor is Eb, not D#), then the accidental the interval needs.
    /// </summary>
    /// <remarks>
    ///     Falls back to the sharp spelling if the letter would need more than a double accidental.
    /// </remarks>
    private static Note.Accidented SpellChordTone(Note.Accidented root, int semitones, IReadOnlySet<int> semitonesFromRoot)
    {
        var pitchClass = (root.PitchClass.Value + semitones) % 12;
        var letterSteps = GetChordToneDegree(semitones % 12, semitonesFromRoot) - 1;
        var letter = NaturalNote.FromValue((root.NaturalNote.Value + letterSteps) % 7);
        var offset = ((pitchClass - letter.PitchClass.Value) % 12 + 18) % 12 - 6; // -6..5

        return offset switch
        {
            0 => new(letter),
            >= -2 and <= 2 => new(letter, Accidental.FromValue(offset)),
            _ => new PitchClass { Value = pitchClass }.ToChromaticNote().ToAccidented()
        };
    }

    /// <summary>
    ///     Gets the degree (1-7, compound degrees reduced: 9 → 2, 11 → 4, 13 → 6) that a chord tone
    ///     <paramref name="semitones" /> (0-11) above the root is spelled as, given the other tones.
    /// </summary>
    private static int GetChordToneDegree(int semitones, IReadOnlySet<int> semitonesFromRoot)
    {
        var hasMajorThird = semitonesFromRoot.Contains(4);
        var hasPerfectFifth = semitonesFromRoot.Contains(7);
        var isDiminishedSeventh = semitonesFromRoot.Contains(3) && semitonesFromRoot.Contains(6) &&
                                  !hasPerfectFifth && !semitonesFromRoot.Contains(10) && !semitonesFromRoot.Contains(11);

        return semitones switch
        {
            0 => 1,
            1 or 2 => 2, // b9, 9 or sus2
            3 => hasMajorThird ? 2 : 3, // #9 beside a major third, otherwise the minor third
            4 => 3,
            5 => 4, // 11 or sus4
            6 => hasPerfectFifth ? 4 : 5, // #11 beside a perfect fifth, otherwise b5
            7 => 5,
            8 => hasPerfectFifth ? 6 : 5, // b13 beside a perfect fifth, otherwise #5
            9 => isDiminishedSeventh ? 7 : 6, // diminished seventh (Bbb in Cdim7), otherwise 6 or 13
            _ => 7
        };
    }

    private static bool TryParseSuffix(string suffix, out ChordFormula? formula)
    {
        var s = suffix.Trim().ToLowerInvariant().Replace(" ", "");
        formula = s switch
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
            "mmaj7" or "minmaj7" or "m(maj7)" =>
                ChordFormula.FromSemitones("Minor major 7th", 3, 7, 11),
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
            _ => null
        };

        return formula is not null;
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
    ///     Gets whether this is an inverted chord (a note other than the root is in the bass)
    /// </summary>
    /// <remarks>
    ///     Compares pitch classes: <see cref="Root" /> keeps the caller's note type while <see cref="Notes" />
    ///     are <see cref="Note.Accidented" />, and records of different types never compare equal.
    /// </remarks>
    public bool IsInverted => Notes[0].PitchClass != Root.PitchClass;

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

        var rotation = (inversion - GetInversion() + Notes.Count) % Notes.Count;
        if (rotation == 0)
        {
            return this;
        }

        var notesList = Notes.ToList();
        var invertedNotes = notesList.Skip(rotation).Concat(notesList.Take(rotation));

        return new(this, new(invertedNotes.ToList()));
    }

    private ChordFormula AnalyzeChordFormula()
    {
        List<ChordFormulaInterval> intervals = [];

        // Measure every note from the root, wherever the root sits in the voicing: skipping the
        // first note skipped the bass, which drops a chord tone from an inverted chord.
        var semitoneValues = Notes
            .Select(note => (note.PitchClass.Value - Root.PitchClass.Value + 12) % 12)
            .Where(semitones => semitones != 0)
            .Distinct();

        foreach (var semitones in semitoneValues)
        {
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

        return symbol + Formula.GetSymbolSuffix();
    }

    public override bool Equals(object? obj) => Equals(obj as Chord);

    public override int GetHashCode() => HashCode.Combine(PitchClassSet, Root);

    public override string ToString() => Symbol;
}
