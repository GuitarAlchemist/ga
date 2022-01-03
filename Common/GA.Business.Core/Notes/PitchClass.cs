namespace GA.Business.Core.Notes;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

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
    private static PitchClass Create(int value) => new() { Value = value };

    public static PitchClass Min => Create(_minValue);
    public static PitchClass Max => Create(_maxValue);
    public static IReadOnlyCollection<PitchClass> All => ValueUtils<PitchClass>.All();

    public static int CheckRange(int value) => ValueUtils<PitchClass>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<PitchClass>.CheckRange(value, minValue, maxValue);
    public static IReadOnlyCollection<PitchClass> GetCollection(int start, int count) => ValueUtils<PitchClass>.Collection(start, count);
    public static implicit operator PitchClass(int value) => Create(value);
    public static implicit operator int(PitchClass octave) => octave.Value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public void CheckMaxValue(int maxValue) => ValueUtils<PitchClass>.CheckRange(Value, _minValue, maxValue);
    public override string ToString() => Value.ToString();

    public Note.Sharp GetNote() => GetSharpNote();
    public Note.Sharp GetSharpNote() => _sharpNotes[_value];
    public Note.Flat GetFlatNote() => _flatNotes[_value];

    private static readonly ImmutableArray<Note.Sharp> _sharpNotes =
        new List<Note.Sharp>
        {
            Note.Sharp.C, Note.Sharp.CSharp, Note.Sharp.D, Note.Sharp.DSharp, Note.Sharp.E, Note.Sharp.F,
            Note.Sharp.FSharp, Note.Sharp.G, Note.Sharp.GSharp, Note.Sharp.A, Note.Sharp.ASharp, Note.Sharp.B
        }.ToImmutableArray();

    private static readonly ImmutableArray<Note.Flat> _flatNotes =
        new List<Note.Flat>
        {
            Note.Flat.C, Note.Flat.DFlat, Note.Flat.D, Note.Flat.EFlat, Note.Flat.E, Note.Flat.F,
            Note.Flat.GFlat, Note.Flat.G, Note.Flat.AFlat, Note.Flat.A, Note.Flat.BFlat, Note.Flat.B
        }.ToImmutableArray();
}