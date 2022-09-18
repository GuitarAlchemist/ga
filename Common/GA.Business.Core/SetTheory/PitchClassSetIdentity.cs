namespace GA.Business.Core.SetTheory;

using Notes;
using GA.Business.Core.Notes.Extensions;
using Scales;
using GA.Business.Core.Fretboard.Primitives;

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
[PublicAPI]
public readonly record struct PitchClassSetIdentity : IMusicObjectCollection<PitchClassSetIdentity>,
                                                      IValueObject<PitchClassSetIdentity>
{
    #region Relational Members

    public int CompareTo(PitchClassSetIdentity other) => Value.CompareTo(other.Value);
    public static bool operator <(PitchClassSetIdentity left, PitchClassSetIdentity right) => left.CompareTo(right) < 0;
    public static bool operator >(PitchClassSetIdentity left, PitchClassSetIdentity right) => left.CompareTo(right) > 0;
    public static bool operator <=(PitchClassSetIdentity left, PitchClassSetIdentity right) =>left.CompareTo(right) <= 0;
    public static bool operator >=(PitchClassSetIdentity left, PitchClassSetIdentity right) => left.CompareTo(right) >= 0;

    #endregion

    public static IReadOnlyCollection<PitchClassSetIdentity> Items => ValueObjectUtils<PitchClassSetIdentity>.Items;
    public static IReadOnlyCollection<int> Values => ValueObjectUtils<PitchClassSetIdentity>.Values;
    public static IEnumerable<PitchClassSetIdentity> Objects => Items;

    private const int _minValue = 0;
    private const int _maxValue = (1 << 12) - 1; // 4096 combinations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PitchClassSetIdentity FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public static PitchClassSetIdentity Min => FromValue(_minValue);
    public static PitchClassSetIdentity Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IValueObject<PitchClassSetIdentity>.EnsureValueInRange(value, _minValue, _maxValue);
    public static IReadOnlyCollection<PitchClassSetIdentity> Collection(int start, int count) => ValueObjectUtils<PitchClassSetIdentity>.GetItems(start, count);

    public static implicit operator PitchClassSetIdentity(int value) => new() { Value = value };
    public static implicit operator int(PitchClassSetIdentity fret) => fret.Value;

    public static PitchClassSetIdentity FromNotes(IEnumerable<Note> notes) => notes.ToPitchClassSet().Identity;
    public static PitchClassSetIdentity FromNotes(params Note[] notes) => FromNotes(notes.ToImmutableArray());

    public static bool ContainsRoot(int value) => (value & 1) == 1; // least significant bit represents the root, which must be present for the Pitch Class Set Identity to be a valid scale

    public PitchClassSet PitchClassSet => PitchClassSet.FromIdentity(this);
    public string ScaleName => ScaleNameByIdentity.Get(this);
    public string ScaleVideoUrl => ScaleVideoUrlByIdentity.Get(this);
    public string ScalePageUrl => $"https://ianring.com/musictheory/scales/{Value}";

    public override string ToString()
    {
        if (string.IsNullOrEmpty(ScaleName)) return $"{Value}";
        return  $"{Value} ({ScaleName})";
    }
}