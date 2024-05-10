using GA.Business.Core.Atonal.Abstractions;

namespace GA.Business.Core.Notes.Primitives;

using Atonal;
using GA.Business.Core.Intervals.Primitives;

/// <summary>
/// A Musical natural note (See https://en.wikipedia.org/wiki/Musical_note, https://en.wikipedia.org/wiki/Natural_(Objects))
/// </summary>
/// <remarks>
/// Implements <see cref="IStaticValueObjectList{NaturalNote}"/>
/// </remarks>
[PublicAPI]
public readonly record struct NaturalNote : IStaticValueObjectList<NaturalNote>, 
                                            IParsable<NaturalNote>,
                                            IPitchClass
{
    #region IStaticReadonlyCollection<NaturalNote> Members

    public static IReadOnlyCollection<NaturalNote> Items => GetItems();
    public static IReadOnlyList<int> Values => Items.ToValueList();

    #endregion

    #region IValueObject<NaturalNote>

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(NaturalNote other) => Value.CompareTo(other.Value);
    public static bool operator <(NaturalNote left, NaturalNote right) => left.CompareTo(right) < 0;
    public static bool operator >(NaturalNote left, NaturalNote right) => left.CompareTo(right) > 0;
    public static bool operator <=(NaturalNote left, NaturalNote right) => left.CompareTo(right) <= 0;
    public static bool operator >=(NaturalNote left, NaturalNote right) => left.CompareTo(right) >= 0;

    #endregion

    #region IPitchClass Members

    /// <summary>
    /// Gets the pitch class of the natural note
    /// </summary>
    /// <remarks>
    /// Major scale:
    /// C: 0
    /// D: 2  (T => +2)
    /// E: 4  (T => +2)
    /// F: 5  (H => +1)
    /// G: 7  (T => +2)
    /// A: 9  (T => +2)
    /// B: 11 (T => +2)
    /// </remarks>
    /// <returns>The <see cref="PitchClass"/></returns>    
    public PitchClass PitchClass => _value switch
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
    
    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 6;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NaturalNote FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static NaturalNote Min => FromValue(_minValue);
    public static NaturalNote Max => FromValue(_maxValue);

    #region Well-known natural notes

    public static NaturalNote C => FromValue(0);
    public static NaturalNote D => FromValue(1);
    public static NaturalNote E => FromValue(2);
    public static NaturalNote F => FromValue(3);
    public static NaturalNote G => FromValue(4);
    public static NaturalNote A => FromValue(5);
    public static NaturalNote B => FromValue(6);
   
    #endregion

    //language=regexp
    public static readonly string RegexPattern = "^(?'note'[A-G])$";
    private static readonly PcreRegex _regex = new(RegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

    /// <inheritdoc />
    public static NaturalNote Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(
        [NotNullWhen(true)] string? s, 
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

    public static implicit operator NaturalNote(int value) => new() {Value = value};
    public static implicit operator int(NaturalNote item) => item.Value;
    public static NaturalNote operator ++(NaturalNote naturalNote) => FromValue((naturalNote.Value + 1) % 7);
    public static NaturalNote operator --(NaturalNote naturalNote) => FromValue((naturalNote.Value - 1) % 7);
    public static NaturalNote operator +(NaturalNote naturalNote, IntervalSize intervalSize) => FromValue((naturalNote.Value + intervalSize.Value - 1) % 7);
    public static IntervalSize operator -(NaturalNote endNote, NaturalNote startNote) => NaturalNoteIntervals.Get(new NaturalNotePair(startNote, endNote));

    public static int CheckRange(int value) => ValueObjectUtils<NaturalNote>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<NaturalNote>.CheckRange(value, minValue, maxValue);

    public IntervalSize GetInterval(NaturalNote other) => NaturalNoteIntervals.Get(new NaturalNotePair(this, other));

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

    /// <summary>
    /// Gets the sharp note from the natural note
    /// </summary>
    /// <returns>The <see cref="Note.Sharp"/></returns>
    public Note.Sharp ToSharpNote() => new(this, SharpAccidental.Sharp);
    
    /// <summary>
    /// Gets the sharp note from the natural note (With specific accidental)
    /// </summary>
    /// <param name="accidental">The <see cref="SharpAccidental"/></param>
    /// <returns>The <see cref="Note.Sharp"/></returns>
    public Note.Sharp ToSharpNote(SharpAccidental accidental) => new(this, accidental);
    
    /// <summary>
    /// Gets the flat note from the natural note
    /// </summary>
    /// <returns>The <see cref="Note.Flat"/></returns>
    public Note.Flat ToFlatNote() => new(this, FlatAccidental.Flat);
    
    /// <summary>
    /// Gets the flat note from the natural note
    /// </summary>
    /// <param name="accidental">The <see cref="FlatAccidental"/></param>
    /// <returns>The <see cref="Note.Flat"/></returns>
    public Note.Flat ToFlatNote(FlatAccidental accidental) => new(this, accidental);

    /// <inheritdoc />
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

    private static IReadOnlyCollection<NaturalNote> GetItems() => ValueObjectUtils<NaturalNote>.Items;

    private class NaturalNoteIntervals() : LazyIndexerBase<NaturalNotePair, IntervalSize>(GetKeyValuePairs())
    {
        private static readonly NaturalNoteIntervals _instance = new();
        public static IntervalSize Get(NaturalNotePair key) => _instance[key];

        private static IEnumerable<KeyValuePair<NaturalNotePair, IntervalSize>> GetKeyValuePairs() =>
            from startNote in Items
            from number in IntervalSize.Items
            let endNote = startNote + number
            select new KeyValuePair<NaturalNotePair, IntervalSize>(new NaturalNotePair(startNote, endNote), number);
    }
}
