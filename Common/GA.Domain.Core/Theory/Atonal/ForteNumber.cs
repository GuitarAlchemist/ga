namespace GA.Domain.Core.Theory.Atonal;

using System.Diagnostics.CodeAnalysis;
using Design.Attributes;
using Design.Schema;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;

/// <summary>
///     Represents a Forte number, which uniquely identifies a pitch class set in music set theory
///     (<see href="https://en.wikipedia.org/wiki/Forte_number" />).
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
[DomainInvariant("Forte number consists of cardinality (0-12) and index (>=1)",
    "Cardinality >= 0 && Cardinality <= 12 && Index >= 1")]
[DomainRelationship(typeof(PitchClassSet), RelationshipType.IsMetadataFor,
    "Identifies the prime form of a pitch class set")]
[DomainRelationship(typeof(SetClass), RelationshipType.IsChildOf)]
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

    /// <summary>
    ///     True when this set class is Z-related — its canonical Forte label carries
    ///     a "Z" marker (e.g. "4-Z29"). Z-related set classes share an interval-class
    ///     vector with a different set class. Defaults to <c>false</c> (the Rahn-ordered
    ///     <see cref="ProgrammaticForteCatalog"/> does not model the Z marker).
    /// </summary>
    public bool IsZRelated { get; init; }

    #region IStaticReadonlyCollection Members

    /// <summary>
    ///     Gets all 4096 possible pitch class sets (See https://harmoniousapp.net/p/0b/Clocks-Pitch-Classes)
    ///     <br /><see cref="IReadOnlyCollection{T}" />
    /// </summary>
    public static IReadOnlyCollection<ForteNumber> Items => AllForteNumbers.Instance;

    #endregion

    /// <inheritdoc />
    public override string ToString() => $"{Cardinality.Value}-{(IsZRelated ? "Z" : "")}{Index}";

    #region Innner Classes

    private class AllForteNumbers : LazyCollectionBase<ForteNumber>
    {
        public static readonly AllForteNumbers Instance = new();

        private AllForteNumbers() : base(Collection(), ", ")
        {
        }

        private static IEnumerable<ForteNumber> Collection()
        {
            // Canonical Forte numbers (Z markers included) for every set class, so display
            // surfaces such as the GraphQL Forte-number hierarchy render "4-Z29", not the
            // bare "4-29". Distinct because T/I-equivalent sets share one Forte number.
            var result = new List<ForteNumber>();
            var seen = new HashSet<(int Card, int Index, bool Z)>();

            void TryAdd(ForteNumber f)
            {
                if (seen.Add((f.Cardinality.Value, f.Index, f.IsZRelated)))
                {
                    result.Add(f);
                }
            }

            foreach (var setClass in SetClass.Items)
            {
                if (ForteCatalog.TryGetForteNumber(setClass.PrimeForm, out var forte))
                {
                    TryAdd(forte);
                }
            }

            // Ensure the trivial classes are present even if SetClass.Items omits them.
            if (TryParse("0-1", null, out var empty)) TryAdd(empty);
            if (TryParse("12-1", null, out var chromatic)) TryAdd(chromatic);

            result.Sort();
            return result;
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
        if (parts.Length != 2 || !int.TryParse(parts[0], out var cardinality))
        {
            return false; // Failure
        }

        // The index may carry a leading "Z" marker for Z-related set classes, e.g. "4-Z29".
        var indexPart = parts[1];
        var isZRelated = indexPart.StartsWith("Z", StringComparison.OrdinalIgnoreCase);
        if (isZRelated)
        {
            indexPart = indexPart[1..];
        }

        if (!int.TryParse(indexPart, out var index))
        {
            return false; // Failure
        }

        // Success
        result = new ForteNumber(cardinality, index) { IsZRelated = isZRelated };
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
