namespace GA.Business.Core.Notes.Primitives;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using PCRE;

using GA.Business.Core.Intervals.Primitives;

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

    static NaturalNote()
    {
        _lazySimpleIntervalDictionary = new(GetIntervalDictionary);
    }

    private const int _minValue = 0;
    private const int _maxValue = 6;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NaturalNote Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

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

    public static IReadOnlyCollection<NaturalNote> All => ValueUtils<NaturalNote>.GetAll();

    public static implicit operator NaturalNote(int value) => new() { Value = value };
    public static implicit operator int(NaturalNote naturalNote) => naturalNote.Value;

    public static NaturalNote operator +(NaturalNote naturalNote, DiatonicNumber diatonicNumber) => Add(naturalNote, diatonicNumber);
    public static DiatonicNumber operator -(NaturalNote naturalNote1, NaturalNote naturalNote2) => GetSimpleInterval(naturalNote1, naturalNote2);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueUtils<NaturalNote>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<NaturalNote>.CheckRange(value, minValue, maxValue);

    public NaturalNote ToDegree(int count) => Create((Value + count) % 7);

    public PitchClass ToPitchClass()
    {
        /*
            See DiatonicScale.Major.Intervals:
            C: 0
            D: 2  (T => +2)
            E: 4  (T => +2)
            F: 5  (H => +1)
            G: 7  (T => +2)
            A: 9  (T => +2)
            B: 11 (T => +2)
         */

        return _value switch
        {
            0 => new() {Value = 0}, // C
            1 => new() {Value = 2}, // D
            2 => new() {Value = 4}, // E
            3 => new() {Value = 5}, // F
            4 => new() {Value = 7}, // G
            5 => new() {Value = 9}, // A
            6 => new() {Value = 11}, // B
            _ => throw new InvalidOperationException()
        };
    }

    public override string ToString() => _value switch
    {
        0 => nameof(C),
        1 => nameof(D),
        2 => nameof(E),
        3 => nameof(F),
        4 => nameof(G),
        5 => nameof(A),
        6 => nameof(B),
        _ => ""
    };

    private static NaturalNote Add(NaturalNote naturalNote, DiatonicNumber diatonicNumber)
    {
        var result = new NaturalNote
        {
            Value = (naturalNote.Value + diatonicNumber.Value - 1) % 7
        };

        return result;
    }

    #region Simple Intervals

    public DiatonicNumber GetSimpleInterval(NaturalNote other) => GetSimpleInterval(this, other);
    private static readonly Lazy<IReadOnlyDictionary<(NaturalNote, NaturalNote), DiatonicNumber>> _lazySimpleIntervalDictionary;
    private static DiatonicNumber GetSimpleInterval(NaturalNote note1, NaturalNote note2) => _lazySimpleIntervalDictionary.Value[(note1, note2)];

    private static ImmutableDictionary<(NaturalNote, NaturalNote), DiatonicNumber> GetIntervalDictionary()
    {
        var dict = new Dictionary<(NaturalNote, NaturalNote), DiatonicNumber>();

        foreach (var note1 in All)
        {
            foreach (var diatonicNumber in DiatonicNumber.All)
            {
                var note2 = note1 + diatonicNumber;
                var tuple = (note1, note2);
                dict[tuple] = diatonicNumber;
            }
        }

        return dict.ToImmutableDictionary();
    }

    #endregion
}

