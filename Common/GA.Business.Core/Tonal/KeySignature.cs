using System.Collections.Immutable;
using GA.Core;

namespace GA.Business.Core.Tonal;

using System.Runtime.CompilerServices;
using Notes;

/// <summary>
/// Key signature (See https://en.wikipedia.org/wiki/Key_signature)
/// </summary>
public readonly record struct KeySignature : IValue<KeySignature>
{
    #region Relational members

    public int CompareTo(KeySignature other) => _value.CompareTo(other._value);
    public static bool operator <(KeySignature left, KeySignature right) => left.CompareTo(right) < 0;
    public static bool operator >(KeySignature left, KeySignature right) => left.CompareTo(right) > 0;
    public static bool operator <=(KeySignature left, KeySignature right) => left.CompareTo(right) <= 0;
    public static bool operator >=(KeySignature left, KeySignature right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -7;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static KeySignature Create(int value) => new() { Value = value };

    public static int CheckRange(int value) => ValueUtils<KeySignature>.CheckRange(value, _minValue, _maxValue);    

    public static KeySignature Min => Create(_minValue);
    public static KeySignature Max => Create(_maxValue);
    public static implicit operator KeySignature(int value) => new() { Value = value };
    public static implicit operator int(KeySignature keySignature) => keySignature.Value;

    public static KeySignature Flat7 => Create(-7);
    public static KeySignature Flat6 => Create(-6);
    public static KeySignature Flat5 => Create(-5);
    public static KeySignature Flat4 => Create(-4);
    public static KeySignature Flat3 => Create(-3);
    public static KeySignature Flat2 => Create(-2);
    public static KeySignature Flat1 => Create(-1);
    public static KeySignature Flat0 => Create(0);
    public static KeySignature Sharp0 => Create(0);
    public static KeySignature Sharp1 => Create(1);
    public static KeySignature Sharp2 => Create(2);
    public static KeySignature Sharp3 => Create(3);
    public static KeySignature Sharp4 => Create(4);
    public static KeySignature Sharp5 => Create(5);
    public static KeySignature Sharp6 => Create(6);
    public static KeySignature Sharp7 => Create(7);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public IReadOnlyCollection<NaturalNote> SharpNotes => GetSharpNotes().AsPrintable();
    public IReadOnlyCollection<NaturalNote> FlatNotes => GetFlatNotes().AsPrintable();

    private IReadOnlyCollection<NaturalNote> GetSharpNotes()
    {
        var result =
            Value <= 0
                ? ImmutableList<NaturalNote>.Empty
                : GetNotes(NaturalNote.F, 4).ToImmutableList(); // See https://en.wikipedia.org/wiki/Circle_of_fifths

        return result;
    }

    private IReadOnlyCollection<NaturalNote> GetFlatNotes()
    {
        var result =
            Value >= 0
                ? ImmutableList<NaturalNote>.Empty
                : GetNotes(NaturalNote.B, 3).ToImmutableList(); // See https://en.wikipedia.org/wiki/Circle_of_fifths

        return result;
    }

    private IEnumerable<NaturalNote> GetNotes(
        NaturalNote firstItem,
        int diatonicInterval)
    {
        var item = firstItem;
        var count = Math.Abs(Value);
        for (var i = 0; i < count; i++)
        {
            yield return item;

            item = item.ToDegree(diatonicInterval);
        }
    }
}