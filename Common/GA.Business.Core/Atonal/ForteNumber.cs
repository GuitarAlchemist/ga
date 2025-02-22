﻿namespace GA.Business.Core.Atonal;

using GA.Core.Collections.Abstractions;
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
/// Implements <see cref="IComparable{ForteNumber}"/> | <see cref="IComparable"/> |<see cref="IParsable{ForteNumber}"/>
/// </remarks>
[PublicAPI]
public readonly record struct ForteNumber : IComparable<ForteNumber>, IComparable, IParsable<ForteNumber>, IStaticReadonlyCollection<ForteNumber>
{
    #region IStaticReadonlyCollection Members

    /// <summary>
    /// Gets all 4096 possible pitch class sets (See https://harmoniousapp.net/p/0b/Clocks-Pitch-Classes)
    /// <br/><see cref="IReadOnlyCollection{PitchClassSet}"/>
    /// </summary>
    public static IReadOnlyCollection<ForteNumber> Items => AllForteNumbers.Instance;

    #endregion

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
   
    /// <summary>
    /// Creates a <see cref="ForteNumber"/> instance
    /// </summary>
    /// <param name="cardinality">The <see cref="Cardinality"/></param>
    /// <param name="index"><see cref="Int32"/> index</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
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
    public override string ToString() => $"{Cardinality.Value}-{Index}";

    #region Innner Classes

    private class AllForteNumbers : LazyCollectionBase<ForteNumber>
    {
        public static readonly AllForteNumbers Instance = new();

        private AllForteNumbers() : base(Collection(), separator: ", ")
        {
        }

        private static IEnumerable<ForteNumber> Collection()
        {
            var setClassesByCardinality = SetClass.Items
                .GroupBy(sc => sc.Cardinality)
                .ToImmutableDictionary(g => g.Key, g => g.Count());
           
            return Enumerable.Range(0, 13)
                .SelectMany(cardinality => Enumerable.Range(1, GetIndexCount(cardinality, setClassesByCardinality))
                    .Select(index => new ForteNumber(cardinality, index)));

            static int GetIndexCount(int cardinality, IReadOnlyDictionary<Cardinality, int> setClassesByCardinality) => cardinality switch
            {
                0 or 1 or 12 => 1,
                _ when setClassesByCardinality.TryGetValue(cardinality, out var count) => count,
                _ => throw new ArgumentOutOfRangeException(nameof(cardinality), "Unexpected cardinality.")
            };
        }
    }

    #endregion
}