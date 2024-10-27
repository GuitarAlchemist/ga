namespace GA.Business.Core.Atonal;

using Primitives;

/// <summary>
/// Represents a Forte number, which uniquely identifies a pitch class set in music set theory.
/// </summary>
/// <remarks>
/// A Forte number consists of two parts:
/// 1. Cardinality: The number of pitch classes in the set (0-12).
/// 2. Index: A unique identifier for sets with the same cardinality.
/// 
/// Forte numbers are used to classify and analyze pitch class sets in atonal music theory.
/// They provide a standardized way to refer to specific pitch class set types.
/// 
/// Example: "3-11" represents the major and minor triads (both have the same Forte number).
/// 
/// This struct implements <see cref="IComparable{ForteNumber}"/>, <see cref="IComparable"/>,
/// and <see cref="IParsable{ForteNumber}"/> for easy comparison and parsing operations.
/// </remarks>
[PublicAPI]
public readonly record struct ForteNumber : IComparable<ForteNumber>, IComparable, IParsable<ForteNumber>
{
    #region IParsable<ForteNumber> Members

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ForteNumber result)
    {
        result = default;

        if (s == null) return false;  // Failure
        var parts = s.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var cardinality) || !int.TryParse(parts[1], out var index)) return false; // Failure

        // Success
        result = new(cardinality, index);
        return true;
    }

    public static ForteNumber Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var result)) throw new FormatException($"'{s}' is not a valid Forte number.");
        return result;
    }

    #endregion

    #region IComparable<ForteNumber>/IComparable Members
    
    public int CompareTo(ForteNumber other)
    {
        var cardinalityComparison = Cardinality.CompareTo(other.Cardinality);
        return cardinalityComparison != 0
            ? cardinalityComparison
            : Index.CompareTo(other.Index);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        return obj is ForteNumber other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(ForteNumber)}");
    }

    #endregion
   
    public ForteNumber(Cardinality cardinality, int index)
    {
        if (cardinality < 0 || cardinality > 12) throw new ArgumentOutOfRangeException(nameof(cardinality), "Cardinality must be between 0 and 12.");
        if (index < 1) throw new ArgumentOutOfRangeException(nameof(index), "Index must be greater than 0.");

        Cardinality = cardinality;
        Index = index;
    }
    
    /// <summary>
    /// Gets the <see cref="Cardinality"/>
    /// </summary>
    public Cardinality Cardinality { get; }

    /// <summary>
    /// Gets the index of the Forte number
    /// </summary>
    public int Index { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Cardinality}-{Index}";
}