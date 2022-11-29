namespace GA.Business.Core.Atonal;

using GA.Core;
using GA.Core.Extensions;
using GA.Core.Collections;
using Primitives;
using Intervals;
using Notes;

/// <summary>
/// All pitches related to each other by octave, enharmonic equivalence, or both (<see href="https://en.wikipedia.org/wiki/Pitch_class"/>
/// </summary>
[PublicAPI]
public readonly record struct PitchClass : IValueObject<PitchClass>,
                                           IValueObjectCollection<PitchClass>,
                                           IIntervalClassType<PitchClass>
{
    #region Relational members

    public int CompareTo(PitchClass other) => _value.CompareTo(other._value);
    public static bool operator <(PitchClass left, PitchClass right) => left.CompareTo(right) < 0;
    public static bool operator >(PitchClass left, PitchClass right) => left.CompareTo(right) > 0;
    public static bool operator <=(PitchClass left, PitchClass right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PitchClass left, PitchClass right) => left.CompareTo(right) >= 0;

    #endregion

    #region NormedType Members

    public static IntervalClass GetNorm(PitchClass item1, PitchClass item2) => IntervalClass.FromValue(item2 - item1);

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 11;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PitchClass FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static PitchClass Min => FromValue(_minValue);
    public static PitchClass Max => FromValue(_maxValue);
    public static IReadOnlyCollection<PitchClass> Items => ValueObjectUtils<PitchClass>.Items;
    public static IReadOnlyCollection<int> Values => Items.ToValues();

    public static implicit operator PitchClass(int value) => FromValue(value);
    public static implicit operator int(PitchClass octave) => octave.Value;
    public static implicit operator Note.SharpKey(PitchClass pitchClass) => pitchClass.ToSharpNote();
    public static implicit operator Note.FlatKey(PitchClass pitchClass) => pitchClass.ToFlatNote();
    public static Interval.Chromatic operator -(PitchClass a, PitchClass b) => a.Value + -b.Value;

    private readonly int _value;

    public int Value
    {
        get => _value;
        init => _value = ValueObjectUtils<PitchClass>.CheckRange(value, _minValue, _maxValue, true);
    }

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<PitchClass>.CheckRange(Value, _minValue, maxValue);

    public override string ToString() => _value switch
    {
        10 => "T",
        11 => "E",
        _ => _value.ToString()
    };

    public Note.Chromatic ToChromaticNote() => _chromaticNotes[_value];
    public Note.SharpKey ToSharpNote() => _sharpNotes[_value];
    public Note.FlatKey ToFlatNote() => _flatNotes[_value];
    public Pitch.Chromatic ToChromaticPitch(Octave octave) => new(ToChromaticNote(), octave);
    public Pitch.Sharp ToSharpPitch(Octave octave) => new(ToSharpNote(), octave);
    public Pitch.Flat ToFlatPitch(Octave octave) => new(ToFlatNote(), octave);

    private static readonly ImmutableList<Note.Chromatic> _chromaticNotes =
        new List<Note.Chromatic>
        {
            Note.Chromatic.C,
            Note.Chromatic.CSharpDb,
            Note.Chromatic.D,
            Note.Chromatic.DSharpEb,
            Note.Chromatic.E,
            Note.Chromatic.F,
            Note.Chromatic.FSharpGb,
            Note.Chromatic.G,
            Note.Chromatic.GSharpAb,
            Note.Chromatic.A,
            Note.Chromatic.ASharpBb,
            Note.Chromatic.B
        }.ToImmutableList();

    private static readonly ImmutableList<Note.SharpKey> _sharpNotes =
        new List<Note.SharpKey>
        {
            Note.SharpKey.C, Note.SharpKey.CSharp, Note.SharpKey.D, Note.SharpKey.DSharp, Note.SharpKey.E, Note.SharpKey.F,
            Note.SharpKey.FSharp, Note.SharpKey.G, Note.SharpKey.GSharp, Note.SharpKey.A, Note.SharpKey.ASharp, Note.SharpKey.B
        }.ToImmutableList();

    private static readonly ImmutableList<Note.FlatKey> _flatNotes =
        new List<Note.FlatKey>
        {
            Note.FlatKey.C, Note.FlatKey.DFlat, Note.FlatKey.D, Note.FlatKey.EFlat, Note.FlatKey.E, Note.FlatKey.F,
            Note.FlatKey.GFlat, Note.FlatKey.G, Note.FlatKey.AFlat, Note.FlatKey.A, Note.FlatKey.BFlat, Note.FlatKey.B
        }.ToImmutableList();
}