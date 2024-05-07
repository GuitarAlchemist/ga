namespace GA.Business.Core.Atonal.Primitives;

using Scales;

/// <summary>
/// Unique identifier for a pitch class set.
/// </summary>
/// <remarks>
/// See https://ianring.com/musictheory/scales/
///
/// Examples:
/// Dorian https://ianring.com/musictheory/scales/1709
/// Phrygian https://ianring.com/musictheory/scales/1451
///
///
/// Links:
///
/// https://mikesimm.djlemonk.com/bblog/Scales-and-Modes.pdf
/// 
/// </remarks>
[PublicAPI]
public readonly record struct PitchClassSetIdentity : IStaticValueObjectList<PitchClassSetIdentity>
{
    #region IStaticValueObjectList<PitchClassSetIdentity> Members

    public static IReadOnlyCollection<PitchClassSetIdentity> Items => ValueObjectUtils<PitchClassSetIdentity>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<PitchClassSetIdentity>.Values;
    public static PitchClassSetIdentity Min => FromValue(_minValue);
    public static PitchClassSetIdentity Max => FromValue(_maxValue);

    #endregion

    #region IValueObject<PitchClassSetIdentity>

    private readonly int _value;
    
    /// <summary>
    /// Gets the base 12 value
    /// </summary>
    /// <remarks>
    /// Range if between 0 and 4095
    /// </remarks>
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational Members

    public int CompareTo(PitchClassSetIdentity other) => Value.CompareTo(other.Value);
    public static bool operator <(PitchClassSetIdentity left, PitchClassSetIdentity right) => left.CompareTo(right) < 0;
    public static bool operator >(PitchClassSetIdentity left, PitchClassSetIdentity right) => left.CompareTo(right) > 0;
    public static bool operator <=(PitchClassSetIdentity left, PitchClassSetIdentity right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PitchClassSetIdentity left, PitchClassSetIdentity right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = (1 << 12) - 1; // 12 tones of the chromatic scale arranged as sets ( ; 0 = omitted, 1 = included) => 4096 combinations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PitchClassSetIdentity FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    /// <summary>
    /// Creates a pitch class set from pitch classes
    /// </summary>
    /// <param name="pitchClasses">The <see cref="IEnumerable{PitchClass}"/></param>
    /// <returns>The <see cref="PitchClassSetIdentity"/></returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pitchClasses"/> is null</exception>
    public static PitchClassSetIdentity FromPitchClasses(IEnumerable<PitchClass> pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        var result = new PitchClassSetIdentity
        {
            Value = GetBase12Value(
                pitchClasses as IReadOnlySet<PitchClass>
                ??
                pitchClasses.ToImmutableHashSet()
            )
        };
        return result;

        static int GetBase12Value(IReadOnlySet<PitchClass> pitchClasses)
        {
            var value = 0;
            var index = 0;
            
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var pitchClass in PitchClass.Items)
            {
                var weight = 1 << index++;
                if (pitchClasses.Contains(pitchClass)) value += weight;
            }

            return value;
        }
    }

    public static int CheckRange(int value) => IRangeValueObject<PitchClassSetIdentity>.EnsureValueInRange(value, _minValue, _maxValue);
    public static IReadOnlyCollection<PitchClassSetIdentity> Collection(int start, int count) => ValueObjectUtils<PitchClassSetIdentity>.GetItems(start, count);

    public static implicit operator PitchClassSetIdentity(int value) => new() { Value = value };
    public static implicit operator int(PitchClassSetIdentity fret) => fret.Value;

    public static bool ContainsRoot(int value) => (value & 1) == 1; // least significant bit represents the root, which must be present for the Pitch Class Set Identity to be a valid scale

    public PitchClassSet PitchClassSet => PitchClassSet.FromIdentity(this);
    public string ScaleName => ScaleNameByIdentity.Get(this);
    public Uri? ScaleVideoUrl => ScaleVideoUrlByIdentity.Get(this);
    public Uri ScalePageUrl => new($"https://ianring.com/musictheory/scales/{Value}");

    /// <inheritdoc />
    public override string ToString() =>string.IsNullOrEmpty(ScaleName) ? $"{Value}" : $"{Value} ({ScaleName})";
}