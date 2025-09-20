namespace GA.Business.Core.Chords;

using Atonal;
using Intervals;

/// <summary>
/// Core chord template representing the essential chord identity based on interval formulas.
/// This follows the same pattern as ScaleMode - built from diatonic intervals rather than arbitrary pitch sets.
/// </summary>
public sealed class ChordTemplate : IEquatable<ChordTemplate>
{
    private readonly Lazy<PitchClassSet> _lazyPitchClassSet;

    /// <summary>Gets the chord formula defining this chord's interval structure</summary>
    public ChordFormula Formula { get; }

    /// <summary>Gets the name of this chord from the formula</summary>
    public string Name => Formula.Name;

    /// <summary>Gets the pitch class set derived from the chord formula</summary>
    public PitchClassSet PitchClassSet => _lazyPitchClassSet.Value;

    /// <summary>Gets the number of notes in this chord</summary>
    public int NoteCount => Formula.Intervals.Count + 1; // +1 for root

    /// <summary>Gets the intervals that define this chord</summary>
    public IReadOnlyCollection<ChordFormulaInterval> Intervals => Formula.Intervals;

    /// <summary>Gets the characteristic intervals (defining chord quality)</summary>
    public IReadOnlyCollection<ChordFormulaInterval> CharacteristicIntervals => 
        Formula.Intervals.Where(i => i.IsEssential).ToList().AsReadOnly();

    /// <summary>Gets the chord quality</summary>
    public ChordQuality Quality => Formula.Quality;

    /// <summary>Gets the chord extension</summary>
    public ChordExtension Extension => Formula.Extension;

    /// <summary>Gets the stacking type</summary>
    public ChordStackingType StackingType => Formula.StackingType;

    /// <summary>
    /// Initializes a new instance of the ChordTemplate class
    /// </summary>
    public ChordTemplate(ChordFormula formula)
    {
        Formula = formula ?? throw new ArgumentNullException(nameof(formula));
        _lazyPitchClassSet = new Lazy<PitchClassSet>(() => CreatePitchClassSet(formula));
    }

    /// <summary>
    /// Creates a chord template from a pitch class set and name
    /// </summary>
    public ChordTemplate(PitchClassSet pitchClassSet, string name)
    {
        if (pitchClassSet == null) throw new ArgumentNullException(nameof(pitchClassSet));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or empty", nameof(name));

        // Analyze the pitch class set to create a formula
        Formula = AnalyzePitchClassSet(pitchClassSet, name);
        _lazyPitchClassSet = new Lazy<PitchClassSet>(() => pitchClassSet);
    }

    /// <summary>
    /// Checks if this chord template contains the specified interval
    /// </summary>
    public bool ContainsInterval(ChordFormulaInterval interval)
    {
        return Formula.Intervals.Contains(interval);
    }

    /// <summary>
    /// Checks if this chord template contains an interval with the specified semitones
    /// </summary>
    public bool ContainsInterval(int semitones)
    {
        return Formula.Intervals.Any(i => i.Interval.Semitones == semitones);
    }

    /// <summary>
    /// Gets the semitone intervals from root (0)
    /// </summary>
    public IReadOnlyList<int> GetSemitoneIntervals()
    {
        return Formula.Intervals
            .Select(interval => interval.Interval.Semitones)
            .OrderBy(semitones => semitones)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets the chord function for a specific interval
    /// </summary>
    public ChordFunction? GetChordFunction(int semitones)
    {
        var interval = Formula.Intervals.FirstOrDefault(i => i.Interval.Semitones == semitones);
        return interval?.Function;
    }

    /// <summary>
    /// Checks if this chord is compatible with the specified scale
    /// </summary>
    public bool IsCompatibleWith(PitchClassSet scale)
    {
        return PitchClassSet.IsSubsetOf(scale);
    }

    /// <summary>
    /// Gets the chord symbol suffix (e.g., "maj7", "m", "dim")
    /// </summary>
    public string GetSymbolSuffix()
    {
        return Formula.GetSymbolSuffix();
    }

    /// <summary>
    /// Creates a new chord template with an additional interval
    /// </summary>
    public ChordTemplate WithInterval(ChordFormulaInterval interval)
    {
        var newFormula = Formula.WithInterval(interval);
        return new ChordTemplate(newFormula);
    }

    /// <summary>
    /// Creates a new chord template without the specified interval function
    /// </summary>
    public ChordTemplate WithoutInterval(ChordFunction function)
    {
        var newFormula = Formula.WithoutInterval(function);
        return new ChordTemplate(newFormula);
    }

    private static PitchClassSet CreatePitchClassSet(ChordFormula formula)
    {
        var pitchClasses = new List<PitchClass> { PitchClass.C }; // Root at C
        
        foreach (var interval in formula.Intervals)
        {
            var pitchClass = PitchClass.FromSemitones(interval.Interval.Semitones);
            pitchClasses.Add(pitchClass);
        }
        
        return new PitchClassSet(pitchClasses);
    }

    private static ChordFormula AnalyzePitchClassSet(PitchClassSet pitchClassSet, string name)
    {
        var pitchClasses = pitchClassSet.ToList();
        if (pitchClasses.Count == 0)
            throw new ArgumentException("Pitch class set cannot be empty", nameof(pitchClassSet));

        // Assume first pitch class is root
        var root = pitchClasses[0];
        var intervals = new List<ChordFormulaInterval>();

        for (int i = 1; i < pitchClasses.Count; i++)
        {
            var semitones = (pitchClasses[i].Value - root.Value + 12) % 12;
            var interval = Interval.FromSemitones(semitones);
            var function = DetermineChordFunction(semitones);
            intervals.Add(new ChordFormulaInterval(interval, function));
        }

        return new ChordFormula(name, intervals);
    }

    private static ChordFunction DetermineChordFunction(int semitones)
    {
        return semitones switch
        {
            2 or 14 => ChordFunction.Ninth,
            3 or 4 => ChordFunction.Third,
            5 or 17 => ChordFunction.Eleventh,
            7 => ChordFunction.Fifth,
            9 or 21 => ChordFunction.Thirteenth,
            10 or 11 => ChordFunction.Seventh,
            _ => ChordFunction.Root
        };
    }

    public bool Equals(ChordTemplate? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return PitchClassSet.Equals(other.PitchClassSet);
    }

    public override bool Equals(object? obj) => Equals(obj as ChordTemplate);
    
    public override int GetHashCode() => PitchClassSet.GetHashCode();
    
    public override string ToString() => $"{Name} ({GetSymbolSuffix()})";
}
