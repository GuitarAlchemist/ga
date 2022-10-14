namespace GA.Business.Core.Notes.Primitives;




using PCRE;

using GA.Business.Core.Intervals.Primitives;
using GA.Core;
using GA.Business.Core.Extensions;
using Atonal;


/// <summary>
/// A Musical natural note (See https://en.wikipedia.org/wiki/Musical_note, https://en.wikipedia.org/wiki/Natural_(Objects))
/// </summary>
[PublicAPI]
public readonly record struct NaturalNote : IValueObject<NaturalNote>, 
                                            IValueObjectCollection<NaturalNote>
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
    public static NaturalNote FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static NaturalNote Min => FromValue(_minValue);
    public static NaturalNote Max => FromValue(_maxValue);

    public static NaturalNote C => FromValue(0);
    public static NaturalNote D => FromValue(1);
    public static NaturalNote E => FromValue(2);
    public static NaturalNote F => FromValue(3);
    public static NaturalNote G => FromValue(4);
    public static NaturalNote A => FromValue(5);
    public static NaturalNote B => FromValue(6);

    //language=regexp
    public static readonly string RegexPattern = "^(?'note'[A-G])$";
    private static readonly PcreRegex _regex = new(RegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

    public static NaturalNote Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }

    public static bool TryParse(
        string? s, 
        IFormatProvider? provider, 
        out NaturalNote result)
    {
        result = default;
        if (string.IsNullOrEmpty(s)) return false;

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
        result = parsedNaturalNote.Value;
        return true;
    }

    public static IReadOnlyCollection<NaturalNote> Items => GetAll();
    public static IReadOnlyCollection<int> Values => Items.ToValues();
    public static IReadOnlyCollection<int> AllValues => Items.Select(note => note.Value).ToImmutableList();

    public static implicit operator NaturalNote(int value) => new() {Value = value};
    public static implicit operator int(NaturalNote item) => item.Value;
    public static NaturalNote operator ++(NaturalNote naturalNote) => FromValue((naturalNote.Value + 1) % 7);
    public static NaturalNote operator --(NaturalNote naturalNote) => FromValue((naturalNote.Value - 1) % 7);
    public static NaturalNote operator +(NaturalNote naturalNote, IntervalSize intervalSize) => FromValue((naturalNote.Value + intervalSize.Value - 1) % 7);
    public static IntervalSize operator -(NaturalNote endNote, NaturalNote startNote) => NaturalNoteIntervals.Get(startNote, endNote);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueObjectUtils<NaturalNote>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<NaturalNote>.CheckRange(value, minValue, maxValue);

    public IntervalSize GetInterval(NaturalNote other) => NaturalNoteIntervals.Get(this, other);

    public IReadOnlyCollection<NaturalNote> GetDegrees(int count)
    {
        var list = new List<NaturalNote>();
        var item = this;
        for (var i = 0; i < count - 1; i++)
        {
            list.Add(item);
            item++;
        }

        return list.AsReadOnly();
    }

    public PitchClass ToPitchClass()
    {
        /*
            Major scale:
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
            0 => new() { Value = 0 }, // C
            1 => new() { Value = 2 }, // D
            2 => new() { Value = 4 }, // E
            3 => new() { Value = 5 }, // F
            4 => new() { Value = 7 }, // G
            5 => new() { Value = 9 }, // A
            6 => new() { Value = 11 }, // B
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
        _ => string.Empty
    };

    private static IReadOnlyCollection<NaturalNote> GetAll() => ValueObjectUtils<NaturalNote>.Items;

    private class NaturalNoteIntervals : LazyIndexerBase<(NaturalNote, NaturalNote), IntervalSize>
    {
        private static readonly NaturalNoteIntervals _instance = new();
        public static IntervalSize Get(NaturalNote startNote, NaturalNote endNote) => _instance[(startNote, endNote)];

        public NaturalNoteIntervals() 
            : base(GetKeyValuePairs())
        {
        }

        private static IEnumerable<KeyValuePair<(NaturalNote, NaturalNote), IntervalSize>> GetKeyValuePairs()
        {
            foreach (var startNote in Items)
            foreach (var number in IntervalSize.Items)
            {
                var endNote = startNote + number;
                yield return new((startNote, endNote),  number);
            }
        }
    }
}
