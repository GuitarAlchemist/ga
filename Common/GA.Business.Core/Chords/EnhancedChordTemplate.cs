namespace GA.Business.Core.Chords;

using Atonal;
using Scales;

/// <summary>
///     Enhanced chord template with scale associations and advanced harmonic analysis
/// </summary>
public class EnhancedChordTemplate : IEquatable<EnhancedChordTemplate>
{
    private readonly HashSet<Scale> _associatedScales = [];
    private readonly Dictionary<PitchClass, ChordTone[]> _chordTonesCache = new();

    /// <summary>
    ///     Initializes a new instance of the EnhancedChordTemplate class
    /// </summary>
    public EnhancedChordTemplate(ChordTemplate coreTemplate)
    {
        CoreTemplate = coreTemplate ?? throw new ArgumentNullException(nameof(coreTemplate));
    }

    /// <summary>
    ///     Initializes a new instance of the EnhancedChordTemplate class from pitch class set
    /// </summary>
    public EnhancedChordTemplate(PitchClassSet pitchClassSet, string name)
    {
        CoreTemplate = ChordTemplate.Analytical.FromPitchClassSet(pitchClassSet, name);
    }

    /// <summary>Gets the core chord template</summary>
    public ChordTemplate CoreTemplate { get; }

    /// <summary>Gets the pitch class set</summary>
    public PitchClassSet PitchClassSet => CoreTemplate.PitchClassSet;

    /// <summary>Gets the chord name</summary>
    public string Name => CoreTemplate.Name;

    /// <summary>Gets the chord formula</summary>
    public ChordFormula Formula => CoreTemplate.Formula;

    /// <summary>Gets the chord quality</summary>
    public ChordQuality Quality => CoreTemplate.Quality;

    /// <summary>Gets the chord extension</summary>
    public ChordExtension Extension => CoreTemplate.Extension;

    /// <summary>Gets the stacking type</summary>
    public ChordStackingType StackingType => CoreTemplate.StackingType;

    /// <summary>Gets the associated scales</summary>
    public IReadOnlySet<Scale> AssociatedScales => _associatedScales.ToHashSet();

    /// <summary>Gets the number of associated scales</summary>
    public int ScaleCount => _associatedScales.Count;

    /// <summary>Gets whether this chord has scale associations</summary>
    public bool HasScaleAssociations => _associatedScales.Count > 0;

    public bool Equals(EnhancedChordTemplate? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return CoreTemplate.Equals(other.CoreTemplate);
    }

    /// <summary>Adds an associated scale to this chord template</summary>
    public void AddAssociatedScale(Scale scale)
    {
        if (scale == null)
        {
            throw new ArgumentNullException(nameof(scale));
        }

        if (IsCompatibleWith(scale))
        {
            _associatedScales.Add(scale);
        }
    }

    /// <summary>Removes an associated scale from this chord template</summary>
    public void RemoveAssociatedScale(Scale scale)
    {
        if (scale != null)
        {
            _associatedScales.Remove(scale);
        }
    }

    /// <summary>Gets whether this chord is compatible with the specified scale</summary>
    public bool IsCompatibleWith(Scale scale)
    {
        if (scale == null)
        {
            return false;
        }

        return CoreTemplate.PitchClassSet.IsSubsetOf(scale.PitchClassSet);
    }

    /// <summary>Gets the chord tones with their harmonic functions</summary>
    public IReadOnlyList<ChordTone> GetChordTones(PitchClass root)
    {
        if (!_chordTonesCache.TryGetValue(root, out var chordTones))
        {
            chordTones = GenerateChordTones(root);
            _chordTonesCache[root] = chordTones;
        }

        return chordTones;
    }

    /// <summary>Gets the most compatible scales for this chord</summary>
    public IEnumerable<Scale> GetMostCompatibleScales()
    {
        return _associatedScales
            .OrderByDescending(scale => CalculateCompatibilityScore(scale))
            .Take(5);
    }

    /// <summary>Gets scales that contain this chord as a subset</summary>
    public IEnumerable<Scale> GetContainingScales()
    {
        return _associatedScales.Where(scale =>
            CoreTemplate.PitchClassSet.IsSubsetOf(scale.PitchClassSet));
    }

    /// <summary>Gets the harmonic function of this chord in the specified scale</summary>
    public HarmonicFunction GetHarmonicFunction(Scale scale, PitchClass root)
    {
        if (!IsCompatibleWith(scale))
        {
            return HarmonicFunction.Unknown;
        }

        var scaleRoot = scale.PitchClassSet.First(); // Assume first pitch class is root
        var intervalFromScaleRoot = (root.Value - scaleRoot.Value + 12) % 12;

        return intervalFromScaleRoot switch
        {
            0 => HarmonicFunction.Tonic,
            2 => HarmonicFunction.Supertonic,
            4 => HarmonicFunction.Mediant,
            5 => HarmonicFunction.Subdominant,
            7 => HarmonicFunction.Dominant,
            9 => HarmonicFunction.Submediant,
            11 => HarmonicFunction.LeadingTone,
            _ => HarmonicFunction.Unknown
        };
    }

    /// <summary>Creates a chord voicing with the specified bass note</summary>
    public ChordVoicing CreateVoicing(PitchClass root, PitchClass? bass = null)
    {
        var chordTones = GetChordTones(root);
        return new ChordVoicing(CoreTemplate, chordTones, bass ?? root);
    }

    /// <summary>Gets tension notes available for this chord in the specified scale</summary>
    public IEnumerable<PitchClass> GetAvailableTensions(Scale scale, PitchClass root)
    {
        if (!IsCompatibleWith(scale))
        {
            return [];
        }

        var chordPitchClasses = GetChordTones(root).Select(ct => ct.PitchClass).ToHashSet();
        var scalePitchClasses = scale.PitchClassSet.ToHashSet();

        return scalePitchClasses.Except(chordPitchClasses);
    }

    private ChordTone[] GenerateChordTones(PitchClass root)
    {
        var chordTones = new List<ChordTone> { new(root, ChordFunction.Root) };

        foreach (var interval in CoreTemplate.Intervals)
        {
            var pitchClass = PitchClass.FromSemitones((root.Value + interval.Interval.Semitones) % 12);
            chordTones.Add(new ChordTone(pitchClass, interval.Function));
        }

        return chordTones.ToArray();
    }

    private double CalculateCompatibilityScore(Scale scale)
    {
        var chordPitchClasses = CoreTemplate.PitchClassSet.Count;
        var scalePitchClasses = scale.PitchClassSet.Count;
        var commonPitchClasses = CoreTemplate.PitchClassSet.Intersect(scale.PitchClassSet).Count();

        // Score based on how many chord tones are in the scale
        return (double)commonPitchClasses / chordPitchClasses;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as EnhancedChordTemplate);
    }

    public override int GetHashCode()
    {
        return CoreTemplate.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Name} (Enhanced)";
    }
}

/// <summary>
///     Represents a chord tone with its harmonic function
/// </summary>
/// <param name="PitchClass">The pitch class of the chord tone</param>
/// <param name="Function">The harmonic function of this tone in the chord</param>
public readonly record struct ChordTone(PitchClass PitchClass, ChordFunction Function);

/// <summary>
///     Represents the harmonic function of a chord in a key
/// </summary>
public enum HarmonicFunction
{
    Unknown,
    Tonic,
    Supertonic,
    Mediant,
    Subdominant,
    Dominant,
    Submediant,
    LeadingTone
}

/// <summary>
///     Represents a specific voicing of a chord
/// </summary>
public class ChordVoicing
{
    /// <summary>
    ///     Initializes a new instance of the ChordVoicing class
    /// </summary>
    public ChordVoicing(ChordTemplate chordTemplate, IEnumerable<ChordTone> chordTones, PitchClass bass)
    {
        ChordTemplate = chordTemplate ?? throw new ArgumentNullException(nameof(chordTemplate));
        ChordTones = chordTones?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(chordTones));
        Bass = bass;
    }

    /// <summary>Gets the chord template</summary>
    public ChordTemplate ChordTemplate { get; }

    /// <summary>Gets the chord tones in this voicing</summary>
    public IReadOnlyList<ChordTone> ChordTones { get; }

    /// <summary>Gets the bass note</summary>
    public PitchClass Bass { get; }

    /// <summary>Gets whether this is an inverted voicing</summary>
    public bool IsInverted => Bass != ChordTones.First().PitchClass;

    /// <summary>Gets the inversion number (0 = root position, 1 = first inversion, etc.)</summary>
    public int GetInversion()
    {
        if (!IsInverted)
        {
            return 0;
        }

        var bassIndex = ChordTones.ToList().FindIndex(ct => ct.PitchClass == Bass);
        return bassIndex == -1 ? 0 : bassIndex;
    }

    public override string ToString()
    {
        return $"{ChordTemplate.Name}{(IsInverted ? $"/{Bass}" : "")}";
    }
}
