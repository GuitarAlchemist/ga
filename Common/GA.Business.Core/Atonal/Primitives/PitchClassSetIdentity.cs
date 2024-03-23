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
/// </remarks>
///
/// TODO: Refactor using PitchClassVariations class to generate identities (Variation index)
[PublicAPI]
public readonly record struct PitchClassSetIdentity : IStaticValueObjectList<PitchClassSetIdentity>
{
    #region IStaticValueObjectList<PitchClassSetIdentity> Members

    public static IReadOnlyCollection<PitchClassSetIdentity> Items => ValueObjectUtils<PitchClassSetIdentity>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<PitchClassSetIdentity>.Values;

    #endregion

    #region IValueObject<PitchClassSetIdentity>

    private readonly int _value;
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
    private const int _maxValue = (1 << 12) - 1; // 4096 combinations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PitchClassSetIdentity FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static PitchClassSetIdentity Min => FromValue(_minValue);
    public static PitchClassSetIdentity Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<PitchClassSetIdentity>.EnsureValueInRange(value, _minValue, _maxValue);
    public static IReadOnlyCollection<PitchClassSetIdentity> Collection(int start, int count) => ValueObjectUtils<PitchClassSetIdentity>.GetItems(start, count);

    public static implicit operator PitchClassSetIdentity(int value) => new() { Value = value };
    public static implicit operator int(PitchClassSetIdentity fret) => fret.Value;

    public static bool ContainsRoot(int value) => (value & 1) == 1; // least significant bit represents the root, which must be present for the Pitch Class Set Identity to be a valid scale

    public PitchClassSet PitchClassSet => PitchClassSet.FromIdentity(this);
    public string ScaleName => ScaleNameByIdentity.Get(this);
    public Uri? ScaleVideoUrl => ScaleVideoUrlByIdentity.Get(this);
    public Uri ScalePageUrl => new($"https://ianring.com/musictheory/scales/{Value}");

    public override string ToString()
    {
        if (string.IsNullOrEmpty(ScaleName)) return $"{Value}";
        return $"{Value} ({ScaleName})";
    }
}