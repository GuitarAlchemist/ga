namespace GA.Business.Core.Atonal;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;
using JetBrains.Annotations;
using Primitives;

/// <summary>
///     Represents a Forte number, which uniquely identifies a pitch class set in music set theory.
/// </summary>
/// <remarks>
///     A Forte number consists of two parts:
///     1. Cardinality: The number of pitch classes in the set (0-12).
///     2. Index: A unique identifier for sets with the same cardinality.
///     Forte numbers are used to classify and analyze pitch class sets in atonal music theory.
///     They provide a standardized way to refer to specific pitch class set types.
///     Example: "3-11" represents the major and minor triads (both have the same Forte number).
///     Implements <see cref="IComparable" /> | <see cref="IComparable" /> |
///     <see cref="IParsable{ForteNumber}" />
/// </remarks>
[PublicAPI]
public readonly record struct ForteNumber :
    IComparable<ForteNumber>,
    IComparable,
    IParsable<ForteNumber>,
    IStaticReadonlyCollection<ForteNumber>
{
    /// <summary>
    ///     Creates a <see cref="ForteNumber" /> instance
    /// </summary>
    /// <param name="cardinality">The <see cref="Cardinality" /></param>
    /// <param name="index"><see cref="Int32" /> index</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public ForteNumber(Cardinality cardinality, int index)
    {
        if (cardinality < 0 || cardinality > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(cardinality), "Cardinality must be between 0 and 12.");
        }

        if (index < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be greater than 0.");
        }

        Cardinality = cardinality;
        Index = index;
    }

    /// <summary>
    ///     Gets the <see cref="Cardinality" />
    /// </summary>
    public Cardinality Cardinality { get; }

    /// <summary>
    ///     Gets the index of the Forte number
    /// </summary>
    public int Index { get; }

    #region IStaticReadonlyCollection Members

    /// <summary>
    ///     Gets all 4096 possible pitch class sets (See https://harmoniousapp.net/p/0b/Clocks-Pitch-Classes)
    ///     <br /><see cref="IReadOnlyCollection{T}" />
    /// </summary>
    public static IReadOnlyCollection<ForteNumber> Items => AllForteNumbers.Instance;

    #endregion

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Cardinality.Value}-{Index}";
    }

    #region Innner Classes

    private class AllForteNumbers : LazyCollectionBase<ForteNumber>
    {
        public static readonly AllForteNumbers Instance = new();

        private AllForteNumbers() : base(Collection(), ", ")
        {
        }

        private static IEnumerable<ForteNumber> Collection()
        {
            var setClassesByCardinality = SetClass.Items
                .GroupBy(sc => sc.Cardinality)
                .ToImmutableDictionary(g => g.Key, g => g.Count());

            return Enumerable.Range(0, 13)
                .SelectMany(cardinality =>
                {
                    var count = GetIndexCount(cardinality, setClassesByCardinality);
                    // If there are no set classes for this cardinality, yield none instead of throwing
                    return count == 0
                        ? []
                        : Enumerable.Range(1, count).Select(index => new ForteNumber(cardinality, index));
                });

            static int GetIndexCount(int cardinality, IReadOnlyDictionary<Cardinality, int> setClassesByCardinality)
            {
                return cardinality switch
                {
                    // For the empty set, the singleton set, and the full chromatic set we have a single class
                    0 or 1 or 12 => 1,
                    // For other cardinalities (2..11), use the number of distinct set classes if available
                    _ when setClassesByCardinality.TryGetValue(cardinality, out var count) => count,
                    // If not available, return 0 to avoid blowing up static initialization in test environments
                    _ => 0
                };
            }
        }
    }

    #endregion

    #region IParsable<ForteNumber> Members

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ForteNumber result)
    {
        result = default;

        if (s == null)
        {
            return false; // Failure
        }

        var parts = s.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var cardinality) || !int.TryParse(parts[1], out var index))
        {
            return false; // Failure
        }

        // Success
        result = new(cardinality, index);
        return true;
    }

    public static ForteNumber Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"'{s}' is not a valid Forte number.");
        }

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
        if (obj is null)
        {
            return 1;
        }

        return obj is ForteNumber other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(ForteNumber)}");
    }

    #endregion
}
