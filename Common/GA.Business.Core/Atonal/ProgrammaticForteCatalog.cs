namespace GA.Business.Core.Atonal;

using System;
using Primitives;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

/// <summary>
/// Programmatically generated Forte-style catalog for all 224 set classes.
/// Uses Rahn ordering (lexicographic by ICV, then by prime form ID) which is
/// mathematically consistent and complete.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is generated lazily on first access and stored in FrozenDictionary
/// for optimal read performance (faster than IReadOnlyDictionary for lookups).
/// </para>
/// <para>
/// <b>Ordering note:</b> This uses Rahn ordering which may differ from Allen Forte's
/// historical 1973 numbering for some set classes. For most practical purposes,
/// the differences are minor and the Rahn ordering is mathematically consistent.
/// </para>
/// </remarks>
[PublicAPI]
public static class ProgrammaticForteCatalog
{
    private static readonly Lazy<FrozenDictionary<PitchClassSetId, ForteNumber>> _lazyForteByPrimeFormId = new(BuildCatalog);
    private static readonly Lazy<FrozenDictionary<ForteNumber, PitchClassSet>> _lazyPrimeFormByForte = new(BuildReverseCatalog);

    /// <summary>
    /// Gets the complete catalog of Forte numbers indexed by prime form ID.
    /// Uses FrozenDictionary for optimal lookup performance.
    /// </summary>
    public static FrozenDictionary<PitchClassSetId, ForteNumber> ForteByPrimeFormId => _lazyForteByPrimeFormId.Value;

    /// <summary>
    /// Gets the complete catalog of prime forms indexed by Forte number.
    /// Uses FrozenDictionary for optimal lookup performance.
    /// </summary>
    public static FrozenDictionary<ForteNumber, PitchClassSet> PrimeFormByForte => _lazyPrimeFormByForte.Value;

    /// <summary>
    /// Attempts to get the Forte number for a given pitch class set.
    /// </summary>
    /// <param name="set">The pitch class set to look up.</param>
    /// <param name="forte">The Forte number if found.</param>
    /// <returns>True if the Forte number was found; otherwise false.</returns>
    public static bool TryGetForteNumber(PitchClassSet set, out ForteNumber forte)
    {
        var primeForm = set.PrimeForm;
        if (primeForm == null)
        {
            forte = default;
            return false;
        }

        return ForteByPrimeFormId.TryGetValue(primeForm.Id, out forte);
    }

    /// <summary>
    /// Gets the Forte number for a given pitch class set, or null if not found.
    /// </summary>
    public static ForteNumber? GetForteNumber(PitchClassSet set)
    {
        return TryGetForteNumber(set, out var forte) ? forte : null;
    }

    /// <summary>
    /// Attempts to get the prime form for a given Forte number.
    /// </summary>
    /// <param name="forte">The Forte number to look up.</param>
    /// <param name="primeForm">The prime form if found.</param>
    /// <returns>True if the prime form was found; otherwise false.</returns>
    public static bool TryGetPrimeForm(ForteNumber forte, out PitchClassSet? primeForm)
    {
        if (PrimeFormByForte.TryGetValue(forte, out primeForm!))
        {
            return true;
        }
        primeForm = null;
        return false;
    }

    /// <summary>
    /// Gets the total number of set classes in the catalog (should be 224).
    /// </summary>
    public static int Count => ForteByPrimeFormId.Count;

    /// <summary>
    /// Gets the number of set classes for a given cardinality.
    /// </summary>
    /// <param name="cardinality">The cardinality (0-12).</param>
    /// <returns>The count of set classes with that cardinality.</returns>
    /// <example>
    /// GetCountForCardinality(3) returns 12 (trichords)
    /// GetCountForCardinality(6) returns 50 (hexachords)
    /// </example>
    public static int GetCountForCardinality(int cardinality)
    {
        return ForteByPrimeFormId.Values.Count(f => f.Cardinality.Value == cardinality);
    }

    /// <summary>
    /// Forces initialization of the catalog. Call this at application startup
    /// to avoid lazy initialization during first use.
    /// </summary>
    public static void PreWarm()
    {
        _ = ForteByPrimeFormId;
        _ = PrimeFormByForte;
    }

    private static FrozenDictionary<PitchClassSetId, ForteNumber> BuildCatalog()
    {
        var result = new Dictionary<PitchClassSetId, ForteNumber>();

        // Group all set classes by cardinality
        var byCardinality = SetClass.Items
            .GroupBy(sc => sc.Cardinality.Value)
            .OrderBy(g => g.Key);

        foreach (var group in byCardinality)
        {
            var cardinality = Cardinality.FromValue(group.Key);

            // Order within cardinality: by ICV (lexicographically), then by prime form ID
            var ordered = group
                .OrderBy(sc => sc.IntervalClassVector.Id)
                .ThenBy(sc => sc.PrimeForm.Id.Value)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var setClass = ordered[i];
                var forte = new ForteNumber(cardinality, i + 1);
                result[setClass.PrimeForm.Id] = forte;
            }
        }

        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<ForteNumber, PitchClassSet> BuildReverseCatalog()
    {
        var result = new Dictionary<ForteNumber, PitchClassSet>();

        foreach (var setClass in SetClass.Items)
        {
            if (ForteByPrimeFormId.TryGetValue(setClass.PrimeForm.Id, out var forte))
            {
                result[forte] = setClass.PrimeForm;
            }
        }

        return result.ToFrozenDictionary();
    }
}

