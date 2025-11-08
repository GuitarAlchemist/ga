namespace GA.Business.Core.Chords;

using Atonal;
using Intervals;
using Intervals.Primitives;
using Notes;

/// <summary>
///     Represents a musical chord with its notes, intervals, and harmonic properties
/// </summary>
public class Chord : IEquatable<Chord>
{
    /// <summary>
    ///     Initializes a new instance of the Chord class
    /// </summary>
    public Chord(Note root, ChordFormula formula, string? symbol = null)
    {
        Root = root;
        Formula = formula;

        // Build notes from formula
        var notes = new List<Note.Accidented> { root.ToAccidented() };
        foreach (var interval in formula.Intervals)
        {
            // Transpose by adding semitones to pitch class value
            var newPitchClassValue = (root.PitchClass.Value + interval.Interval.Semitones.Value) % 12;
            var newNote = new PitchClass { Value = newPitchClassValue }.ToChromaticNote().ToAccidented();
            notes.Add(newNote);
        }

        Notes = new AccidentedNoteCollection(notes);
        PitchClassSet = Notes.ToPitchClassSet();

        Quality = DetermineQuality();
        Extension = DetermineExtension();
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
        Root = root ?? notes.First();
        PitchClassSet = notes.ToPitchClassSet();

        // Analyze the chord to determine formula
        Formula = AnalyzeChordFormula();
        Quality = DetermineQuality();
        Extension = DetermineExtension();
        Symbol = GenerateSymbol();
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
    ///     Gets the chord quality (major, minor, diminished, etc.)
    /// </summary>
    public ChordQuality Quality { get; }

    /// <summary>
    ///     Gets the chord extension (7th, 9th, 11th, 13th)
    /// </summary>
    public ChordExtension Extension { get; }

    /// <summary>
    ///     Gets the pitch class set representation of the chord
    /// </summary>
    public PitchClassSet PitchClassSet { get; }

    /// <summary>
    ///     Gets whether this is an inverted chord
    /// </summary>
    public bool IsInverted => Notes.First() != Root;

    /// <summary>
    ///     Gets the bass note (lowest note in the voicing)
    /// </summary>
    public Note Bass => Notes.First();

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
    ///     Creates a chord from a chord symbol (e.g., "Cmaj7", "Am", "F#dim")
    /// </summary>
    public static Chord FromSymbol(string symbol)
    {
        var parser = new ChordSymbolParser();
        return parser.Parse(symbol);
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
        return rootIndex == -1 ? 0 : rootIndex;
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

        return new Chord(new AccidentedNoteCollection(invertedNotes), Root);
    }

    private ChordFormula AnalyzeChordFormula()
    {
        var intervals = new List<ChordFormulaInterval>();

        foreach (var note in Notes.Skip(1)) // Skip root
        {
            var semitones = (note.PitchClass.Value - Root.PitchClass.Value + 12) % 12;
            var interval = new Interval.Chromatic(Semitones.FromValue(semitones));
            var function = DetermineChordFunction(interval);
            intervals.Add(new ChordFormulaInterval(interval, function));
        }

        return new ChordFormula($"Analyzed_{Root}", intervals);
    }

    private ChordFunction DetermineChordFunction(Interval interval)
    {
        return interval.Semitones.Value switch
        {
            3 or 4 => ChordFunction.Third,
            7 => ChordFunction.Fifth,
            10 or 11 => ChordFunction.Seventh,
            2 or 14 => ChordFunction.Ninth,
            5 or 17 => ChordFunction.Eleventh,
            9 or 21 => ChordFunction.Thirteenth,
            _ => ChordFunction.Root
        };
    }

    private ChordQuality DetermineQuality()
    {
        var hasMinorThird = Formula.Intervals.Any(i => i.Interval.Semitones.Value == 3);
        var hasMajorThird = Formula.Intervals.Any(i => i.Interval.Semitones.Value == 4);
        var hasDiminishedFifth = Formula.Intervals.Any(i => i.Interval.Semitones.Value == 6);
        var hasAugmentedFifth = Formula.Intervals.Any(i => i.Interval.Semitones.Value == 8);

        if (hasDiminishedFifth && hasMinorThird)
        {
            return ChordQuality.Diminished;
        }

        if (hasAugmentedFifth && hasMajorThird)
        {
            return ChordQuality.Augmented;
        }

        if (hasMinorThird)
        {
            return ChordQuality.Minor;
        }

        if (hasMajorThird)
        {
            return ChordQuality.Major;
        }

        return ChordQuality.Other;
    }

    private ChordExtension DetermineExtension()
    {
        var hasSeventh = Formula.Intervals.Any(i => i.Interval.Semitones.Value is 10 or 11);
        var hasNinth = Formula.Intervals.Any(i => i.Interval.Semitones.Value is 2 or 14);
        var hasEleventh = Formula.Intervals.Any(i => i.Interval.Semitones.Value is 5 or 17);
        var hasThirteenth = Formula.Intervals.Any(i => i.Interval.Semitones.Value is 9 or 21);

        if (hasThirteenth)
        {
            return ChordExtension.Thirteenth;
        }

        if (hasEleventh)
        {
            return ChordExtension.Eleventh;
        }

        if (hasNinth && hasSeventh)
        {
            return ChordExtension.Ninth;
        }

        if (hasNinth && !hasSeventh)
        {
            return ChordExtension.Add9;
        }

        if (hasSeventh)
        {
            return ChordExtension.Seventh;
        }

        return ChordExtension.Triad;
    }

    private string GenerateSymbol()
    {
        var symbol = Root.ToString();

        symbol += Quality switch
        {
            ChordQuality.Minor => "m",
            ChordQuality.Diminished => "dim",
            ChordQuality.Augmented => "aug",
            ChordQuality.Major => "",
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

    public override bool Equals(object? obj)
    {
        return Equals(obj as Chord);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PitchClassSet, Root);
    }

    public override string ToString()
    {
        return Symbol;
    }
}

/// <summary>
///     Represents the quality of a chord
/// </summary>
public enum ChordQuality
{
    Major,
    Minor,
    Diminished,
    Augmented,
    Suspended,
    Other
}
