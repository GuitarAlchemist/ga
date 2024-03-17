namespace GA.Business.Core.Atonal;

using GA.Core.Extensions;
using GA.Core.Collections;
using Primitives;
using Intervals;
using Notes;
using GA.Core;

/// <summary>
/// Items pitches related to each other by octave, enharmonic equivalence, or both (<see href="https://en.wikipedia.org/wiki/Pitch_class"/>
/// </summary>
/// <remarks>
/// Implements <see cref="IStaticValueObjectList{PitchClass}"/>, <see cref="IStaticNorm{PitchClass, IntervalClass}"/>
/// </remarks>
[PublicAPI]
public readonly record struct PitchClass : IStaticValueObjectList<PitchClass>,
                                           IStaticNorm<PitchClass, IntervalClass>
{
    #region IStaticValueObjectList<PitchClass> Members

    public static IReadOnlyCollection<PitchClass> Items => ValueObjectUtils<PitchClass>.Items;
    public static IReadOnlyList<int> Values => Items.ToValueList();

    #endregion

    #region IStaticIntervalClassNorm<PitchClass> Members

    public static IntervalClass GetNorm(PitchClass item1, PitchClass item2) => IntervalClass.FromValue(Math.Abs(item2.Value - item1.Value));

    #endregion

    #region IValueObject<PitchClass> Members

    private readonly int _value;

    public int Value
    {
        get => _value;
        init => _value = ValueObjectUtils<PitchClass>.CheckRange(value, _minValue, _maxValue, true);
    }

    #endregion

    #region Relational members

    public int CompareTo(PitchClass other) => _value.CompareTo(other._value);
    public static bool operator <(PitchClass left, PitchClass right) => left.CompareTo(right) < 0;
    public static bool operator >(PitchClass left, PitchClass right) => left.CompareTo(right) > 0;
    public static bool operator <=(PitchClass left, PitchClass right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PitchClass left, PitchClass right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 11;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PitchClass FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static PitchClass Min => FromValue(_minValue);
    public static PitchClass Max => FromValue(_maxValue);
    public static implicit operator PitchClass(int value) => FromValue(value);
    public static implicit operator int(PitchClass octave) => octave.Value;
    public static implicit operator Note.Sharp(PitchClass pitchClass) => pitchClass.ToSharpNote();
    public static implicit operator Note.Flat(PitchClass pitchClass) => pitchClass.ToFlatNote();
    public static Interval.Chromatic operator -(PitchClass a, PitchClass b) => a.Value + -b.Value;

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<PitchClass>.CheckRange(Value, _minValue, maxValue);

    public override string ToString() => _value switch
    {
        10 => "T",
        11 => "Em",
        _ => _value.ToString()
    };

    public Note.Chromatic ToChromaticNote() => _chromaticNotes[_value];
    public Note.Sharp ToSharpNote() => _sharpNotes[_value];
    public Note.Flat ToFlatNote() => _flatNotes[_value];
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

    private static readonly ImmutableList<Note.Sharp> _sharpNotes =
        new List<Note.Sharp>
        {
            Note.Sharp.C, Note.Sharp.CSharp, Note.Sharp.D, Note.Sharp.DSharp, Note.Sharp.E, Note.Sharp.F,
            Note.Sharp.FSharp, Note.Sharp.G, Note.Sharp.GSharp, Note.Sharp.A, Note.Sharp.ASharp, Note.Sharp.B
        }.ToImmutableList();

    private static readonly ImmutableList<Note.Flat> _flatNotes =
        new List<Note.Flat>
        {
            Note.Flat.C, Note.Flat.DFlat, Note.Flat.D, Note.Flat.EFlat, Note.Flat.E, Note.Flat.F,
            Note.Flat.GFlat, Note.Flat.G, Note.Flat.AFlat, Note.Flat.A, Note.Flat.BFlat, Note.Flat.B
        }.ToImmutableList();
}