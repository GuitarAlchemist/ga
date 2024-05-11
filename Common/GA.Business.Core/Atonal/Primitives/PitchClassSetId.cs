namespace GA.Business.Core.Atonal.Primitives;

/// <summary>
/// A pitch class set ID
/// </summary>
/// <remarks>
///  12 tones of the chromatic scale arranged as sets<br/>
///  Each note is included or excluded in a set (0 = omitted, 1 = included)
///  2^12 => 4096 combinations
/// </remarks>
[PublicAPI]
public readonly record struct PitchClassSetId : IStaticReadonlyCollectionFromValues<PitchClassSetId>
{
    #region IStaticReadonlyCollectionFromValues<PitchClassSetId> Members
    
    public static implicit operator PitchClassSetId(int value) => new() { Value = value };
    public static implicit operator int(PitchClassSetId pitchClassSetId) => pitchClassSetId._value;

    public static IReadOnlyCollection<PitchClassSetId> Items => IStaticReadonlyCollectionFromValues<PitchClassSetId>.Items;

    /// <inheritdoc />
    public static PitchClassSetId Min => FromValue(_minValue);

    /// <inheritdoc />
    public static PitchClassSetId Max => FromValue(_maxValue);
    
    /// <inheritdoc />
    public static PitchClassSetId FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };
    
    /// <inheritdoc />
    public int Value
    {
        get => _value; 
        private init => _value = ValueObjectUtils<PitchClassSetId>.CheckRange(value, _minValue, _maxValue);
    }

    private const int _minValue = 0;
    private const int _maxValue = 4095;
    private readonly int _value;
   
    #endregion

    #region Equality Members

    /// <inheritdoc />
    public bool Equals(PitchClassSetId other) => _value == other._value;

    /// <inheritdoc />
    public override int GetHashCode() => _value;    

    #endregion
    
    #region Relational Members

    public static bool operator <(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) < 0;
    public static bool operator >(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) > 0;
    public static bool operator <=(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(PitchClassSetId other) => _value.CompareTo(other._value);

    #endregion
}