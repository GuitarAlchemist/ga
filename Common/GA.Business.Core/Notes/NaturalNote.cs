using System.Runtime.CompilerServices;
using PCRE;

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

    //language=regexp
    public static readonly string RegexPattern = "^(?'note'[A-G])$";
    private static readonly PcreRegex _regex = new(RegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

    public static bool TryParse(string s, out NaturalNote naturalNote)
    {
        naturalNote = default;
        var match = _regex.Match(s);
        if (!match.Success) return false; // Failure

        var noteGroup = match.Groups["note"];
        NaturalNote? parsedNaturalNote = noteGroup.Value.ToUpperInvariant() switch
        {
            nameof(C) => C,
            nameof(D) => D,
            nameof(E) => E,
            nameof(F) => F,
            nameof(G) => G,
            nameof(A) => A,
            nameof(B) => B,
            _ => null
        };

        if (!parsedNaturalNote.HasValue) return false; // Failure

        // Success
        naturalNote = parsedNaturalNote.Value;
        return true;
    }

    public static IReadOnlyCollection<NaturalNote> All => ValueUtils<NaturalNote>.All();

    public static implicit operator NaturalNote(int value) => new() { Value = value };
    public static implicit operator int(NaturalNote naturalNote) => naturalNote.Value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueUtils<NaturalNote>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<NaturalNote>.CheckRange(value, minValue, maxValue);

    public NaturalNote ToDegree(int diatonicInterval) => Create((Value + diatonicInterval) % 7);

    public PitchClass GetPitchClass() => _value switch
    {
        0 => 0,
        1 => 2,
        2 => 4,
        3 => 5,
        4 => 7,
        5 => 9,
        6 => 11,
        _ => throw new InvalidOperationException()
    };

    public override string ToString() => _value switch
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

