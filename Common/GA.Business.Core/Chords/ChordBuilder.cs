namespace GA.Business.Core.Chords;

using Notes;
using Intervals;
using Tonal.Modes;

/// <summary>
/// Builder class for creating chords with fluent API
/// </summary>
public class ChordBuilder
{
    private Note? _root;
    private readonly List<ChordFormulaInterval> _intervals = new();
    private ChordStackingType _stackingType = ChordStackingType.Tertian;
    private string? _name;

    /// <summary>
    /// Sets the root note of the chord
    /// </summary>
    public ChordBuilder WithRoot(Note root)
    {
        _root = root;
        return this;
    }

    /// <summary>
    /// Sets the root note of the chord from a string
    /// </summary>
    public ChordBuilder WithRoot(string rootName)
    {
        _root = Note.Parse(rootName);
        return this;
    }

    /// <summary>
    /// Adds an interval to the chord
    /// </summary>
    public ChordBuilder WithInterval(Interval interval, ChordFunction function, bool isEssential = true)
    {
        _intervals.Add(new ChordFormulaInterval(interval, function, isEssential));
        return this;
    }

    /// <summary>
    /// Adds an interval to the chord by semitones
    /// </summary>
    public ChordBuilder WithInterval(int semitones, ChordFunction function, bool isEssential = true)
    {
        return WithInterval(Interval.FromSemitones(semitones), function, isEssential);
    }

    /// <summary>
    /// Adds a major third to the chord
    /// </summary>
    public ChordBuilder WithMajorThird()
    {
        return WithInterval(4, ChordFunction.Third);
    }

    /// <summary>
    /// Adds a minor third to the chord
    /// </summary>
    public ChordBuilder WithMinorThird()
    {
        return WithInterval(3, ChordFunction.Third);
    }

    /// <summary>
    /// Adds a perfect fifth to the chord
    /// </summary>
    public ChordBuilder WithPerfectFifth()
    {
        return WithInterval(7, ChordFunction.Fifth);
    }

    /// <summary>
    /// Adds a diminished fifth to the chord
    /// </summary>
    public ChordBuilder WithDiminishedFifth()
    {
        return WithInterval(6, ChordFunction.Fifth);
    }

    /// <summary>
    /// Adds an augmented fifth to the chord
    /// </summary>
    public ChordBuilder WithAugmentedFifth()
    {
        return WithInterval(8, ChordFunction.Fifth);
    }

    /// <summary>
    /// Adds a major seventh to the chord
    /// </summary>
    public ChordBuilder WithMajorSeventh()
    {
        return WithInterval(11, ChordFunction.Seventh);
    }

    /// <summary>
    /// Adds a minor seventh to the chord
    /// </summary>
    public ChordBuilder WithMinorSeventh()
    {
        return WithInterval(10, ChordFunction.Seventh);
    }

    /// <summary>
    /// Adds a diminished seventh to the chord
    /// </summary>
    public ChordBuilder WithDiminishedSeventh()
    {
        return WithInterval(9, ChordFunction.Seventh);
    }

    /// <summary>
    /// Adds a ninth to the chord
    /// </summary>
    public ChordBuilder WithNinth()
    {
        return WithInterval(14, ChordFunction.Ninth);
    }

    /// <summary>
    /// Adds a flat ninth to the chord
    /// </summary>
    public ChordBuilder WithFlatNinth()
    {
        return WithInterval(13, ChordFunction.Ninth);
    }

    /// <summary>
    /// Adds a sharp ninth to the chord
    /// </summary>
    public ChordBuilder WithSharpNinth()
    {
        return WithInterval(15, ChordFunction.Ninth);
    }

    /// <summary>
    /// Adds an eleventh to the chord
    /// </summary>
    public ChordBuilder WithEleventh()
    {
        return WithInterval(17, ChordFunction.Eleventh);
    }

    /// <summary>
    /// Adds a sharp eleventh to the chord
    /// </summary>
    public ChordBuilder WithSharpEleventh()
    {
        return WithInterval(18, ChordFunction.Eleventh);
    }

    /// <summary>
    /// Adds a thirteenth to the chord
    /// </summary>
    public ChordBuilder WithThirteenth()
    {
        return WithInterval(21, ChordFunction.Thirteenth);
    }

    /// <summary>
    /// Adds a flat thirteenth to the chord
    /// </summary>
    public ChordBuilder WithFlatThirteenth()
    {
        return WithInterval(20, ChordFunction.Thirteenth);
    }

    /// <summary>
    /// Sets the stacking type of the chord
    /// </summary>
    public ChordBuilder WithStackingType(ChordStackingType stackingType)
    {
        _stackingType = stackingType;
        return this;
    }

    /// <summary>
    /// Sets the name of the chord
    /// </summary>
    public ChordBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Creates a major triad
    /// </summary>
    public ChordBuilder AsMajorTriad()
    {
        _intervals.Clear();
        return WithMajorThird().WithPerfectFifth().WithName("Major");
    }

    /// <summary>
    /// Creates a minor triad
    /// </summary>
    public ChordBuilder AsMinorTriad()
    {
        _intervals.Clear();
        return WithMinorThird().WithPerfectFifth().WithName("Minor");
    }

    /// <summary>
    /// Creates a diminished triad
    /// </summary>
    public ChordBuilder AsDiminishedTriad()
    {
        _intervals.Clear();
        return WithMinorThird().WithDiminishedFifth().WithName("Diminished");
    }

    /// <summary>
    /// Creates an augmented triad
    /// </summary>
    public ChordBuilder AsAugmentedTriad()
    {
        _intervals.Clear();
        return WithMajorThird().WithAugmentedFifth().WithName("Augmented");
    }

    /// <summary>
    /// Creates a dominant seventh chord
    /// </summary>
    public ChordBuilder AsDominantSeventh()
    {
        return AsMajorTriad().WithMinorSeventh().WithName("Dominant 7th");
    }

    /// <summary>
    /// Creates a major seventh chord
    /// </summary>
    public ChordBuilder AsMajorSeventh()
    {
        return AsMajorTriad().WithMajorSeventh().WithName("Major 7th");
    }

    /// <summary>
    /// Creates a minor seventh chord
    /// </summary>
    public ChordBuilder AsMinorSeventh()
    {
        return AsMinorTriad().WithMinorSeventh().WithName("Minor 7th");
    }

    /// <summary>
    /// Creates a half-diminished seventh chord
    /// </summary>
    public ChordBuilder AsHalfDiminishedSeventh()
    {
        return AsDiminishedTriad().WithMinorSeventh().WithName("Half Diminished 7th");
    }

    /// <summary>
    /// Creates a fully diminished seventh chord
    /// </summary>
    public ChordBuilder AsFullyDiminishedSeventh()
    {
        return AsDiminishedTriad().WithDiminishedSeventh().WithName("Fully Diminished 7th");
    }

    /// <summary>
    /// Creates a chord from a scale mode and degree
    /// </summary>
    public ChordBuilder FromScaleMode(ScaleMode mode, int degree, ChordExtension extension = ChordExtension.Triad)
    {
        if (degree < 1 || degree > mode.Notes.Count)
            throw new ArgumentOutOfRangeException(nameof(degree));

        var rootNote = mode.Notes[degree - 1];
        WithRoot(rootNote);

        // Build chord based on scale degrees
        var scaleNotes = mode.Notes.ToList();
        var rootIndex = degree - 1;

        // Add third
        var thirdIndex = (rootIndex + 2) % scaleNotes.Count;
        var thirdInterval = Interval.FromSemitones((scaleNotes[thirdIndex].MidiNote - rootNote.MidiNote) % 12);
        WithInterval(thirdInterval, ChordFunction.Third);

        // Add fifth
        var fifthIndex = (rootIndex + 4) % scaleNotes.Count;
        var fifthInterval = Interval.FromSemitones((scaleNotes[fifthIndex].MidiNote - rootNote.MidiNote) % 12);
        WithInterval(fifthInterval, ChordFunction.Fifth);

        // Add extensions if requested
        if (extension >= ChordExtension.Seventh)
        {
            var seventhIndex = (rootIndex + 6) % scaleNotes.Count;
            var seventhInterval = Interval.FromSemitones((scaleNotes[seventhIndex].MidiNote - rootNote.MidiNote) % 12);
            WithInterval(seventhInterval, ChordFunction.Seventh);
        }

        WithName($"{mode.Name} {degree} {extension}");
        return this;
    }

    /// <summary>
    /// Builds the chord
    /// </summary>
    public Chord Build()
    {
        if (_root == null)
            throw new InvalidOperationException("Root note must be specified");

        var formula = new ChordFormula(_name ?? "Custom", _intervals, _stackingType);
        return new Chord(_root, formula);
    }

    /// <summary>
    /// Creates a new chord builder
    /// </summary>
    public static ChordBuilder Create() => new();

    /// <summary>
    /// Creates a chord builder with the specified root
    /// </summary>
    public static ChordBuilder Create(Note root) => new ChordBuilder().WithRoot(root);

    /// <summary>
    /// Creates a chord builder with the specified root
    /// </summary>
    public static ChordBuilder Create(string rootName) => new ChordBuilder().WithRoot(rootName);
}
