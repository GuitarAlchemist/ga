using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using GA.Business.Core.Intervals;

namespace GA.Business.Core.Notes.Primitives;

/// <inheritdoc cref="IEquatable{PitchClass}" />
/// <inheritdoc cref="IComparable{PitchClass}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// A pitch class between 0 and 11 (<see href="https://en.wikipedia.org/wiki/Pitch_class"/>
/// </summary>
[PublicAPI]
public readonly record struct PitchClass : IValue<PitchClass>, IAll<PitchClass>
{
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
    private static PitchClass Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static PitchClass Min => Create(_minValue);
    public static PitchClass Max => Create(_maxValue);
    public static IReadOnlyCollection<PitchClass> All => ValueUtils<PitchClass>.All();

    public static int CheckRange(int value) => ValueUtils<PitchClass>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<PitchClass>.CheckRange(value, minValue, maxValue);
    public static IReadOnlyCollection<PitchClass> GetCollection(int start, int count) => ValueUtils<PitchClass>.Collection(start, count);
    public static implicit operator PitchClass(int value) => Create(value);
    public static implicit operator int(PitchClass octave) => octave.Value;
    public static implicit operator Note.Sharp(PitchClass pitchClass) => pitchClass.ToSharpNote();
    public static implicit operator Note.Flat(PitchClass pitchClass) => pitchClass.ToFlatNote();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public void CheckMaxValue(int maxValue) => ValueUtils<PitchClass>.CheckRange(Value, _minValue, maxValue);
    public override string ToString() => Value.ToString();

    public Note.Chromatic ToChromaticNote() => _chromaticNotes[_value];
    public Note.Sharp ToSharpNote() => _sharpNotes[_value];
    public Note.Flat ToFlatNote() => _flatNotes[_value];
    public Pitch.Chromatic ToChromaticPitch(Octave octave) => new(_chromaticNotes[_value], octave);
    public Pitch.Sharp ToSharpPitch(Octave octave) => new(_sharpNotes[_value], octave);
    public Pitch.Flat ToFlatPitch(Octave octave) => new(_flatNotes[_value], octave);

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