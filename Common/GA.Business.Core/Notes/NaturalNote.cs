using System.Runtime.CompilerServices;

namespace GA.Business.Core.Notes;

/// <inheritdoc cref="IEquatable{Noteing}" />
/// <inheritdoc cref="IComparable{Noteing}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// A musical natural note (<href="https://en.wikipedia.org/wiki/Musical_note"></href>, <href="https://en.wikipedia.org/wiki/Natural_(music)"></href>)
/// </summary>
[PublicAPI]
public readonly record struct NaturalNote : IValue<NaturalNote>, IAll<NaturalNote>
{
    #region Relational members

    public int CompareTo(NaturalNote other) => Value.CompareTo(other.Value);
    public static bool operator <(NaturalNote left, NaturalNote right) => left.CompareTo(right) < 0;
    public static bool operator >(NaturalNote left, NaturalNote right) => left.CompareTo(right) > 0;
    public static bool operator <=(NaturalNote left, NaturalNote right) => left.CompareTo(right) <= 0;
    public static bool operator >=(NaturalNote left, NaturalNote right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 6;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NaturalNote Create(int value) => new() { Value = value };

    public static NaturalNote Min => Create(_minValue);
    public static NaturalNote Max => Create(_maxValue);

    public static NaturalNote C => Create(0);
    public static NaturalNote D => Create(1);
    public static NaturalNote E => Create(2);
    public static NaturalNote F => Create(3);
    public static NaturalNote G => Create(4);
    public static NaturalNote A => Create(5);
    public static NaturalNote B => Create(6);

    public static IReadOnlyCollection<NaturalNote> All => ValueUtils<NaturalNote>.All();

    public static NaturalNote operator ++(NaturalNote note) => Create(note.Value + 1);
    public static NaturalNote operator --(NaturalNote note) => Create(note.Value - 1);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueUtils<NaturalNote>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<NaturalNote>.CheckRange(value, minValue, maxValue);

    public override string ToString()
    {
        return Value switch
        {
            0 => "C",
            1 => "D",
            2 => "E",
            3 => "F",
            4 => "G",
            5 => "A",
            6 => "B",
            _ => ""
        };
    }
}

