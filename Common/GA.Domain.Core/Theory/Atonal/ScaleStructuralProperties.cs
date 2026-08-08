namespace GA.Domain.Core.Theory.Atonal;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
///     Specifies Rothenberg propriety classifications for pitch class sets.
/// </summary>
public enum RothenbergPropriety
{
    StrictlyProper,
    Proper,
    Improper
}

/// <summary>
///     Specifies Zeitler scale legitimacy verdicts.
/// </summary>
public enum ZeitlerLegitimacy
{
    LegitimateScale,
    Borderline,
    PitchCollection
}

/// <summary>
///     Provides mathematical and theoretical structural properties for pitch class sets and scales.
///     Includes Myhill's Property, Rothenberg Propriety, Maximal Evenness, Well-Formedness,
///     Imperfection count, and Zeitler Legitimacy checks.
/// </summary>
public static class ScaleStructuralProperties
{
    /// <summary>
    ///     Checks if a pitch class set has Myhill's Property.
    ///     A scale has Myhill's Property if every generic interval (1..cardinality-1)
    ///     takes on exactly two distinct specific interval sizes (in semitones).
    /// </summary>
    public static bool HasMyhillProperty(this PitchClassSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var card = set.Cardinality.Value;
        if (card < 2 || card >= 12) return false;

        var pcs = set.Select(pc => pc.Value).OrderBy(v => v).ToList();

        for (var generic = 1; generic < card; generic++)
        {
            var specificSizes = new HashSet<int>();
            for (var i = 0; i < card; i++)
            {
                var targetIdx = (i + generic) % card;
                var startPitch = pcs[i];
                var endPitch = pcs[targetIdx];
                var semitones = (endPitch - startPitch + 12) % 12;
                if (semitones == 0) semitones = 12; // generic interval wrapping around octave
                specificSizes.Add(semitones);
            }

            if (specificSizes.Count != 2)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Evaluates the Rothenberg Propriety of a pitch class set.
    ///     A scale is strictly proper if for any generic intervals g1 &lt; g2, every specific interval of g1 is strictly smaller than every specific interval of g2.
    ///     It is proper if every specific interval of g1 is &lt;= every specific interval of g2.
    ///     Otherwise, it is improper.
    /// </summary>
    public static RothenbergPropriety GetRothenbergPropriety(this PitchClassSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var card = set.Cardinality.Value;
        if (card < 2) return RothenbergPropriety.StrictlyProper;

        var pcs = set.Select(pc => pc.Value).OrderBy(v => v).ToList();
        var genericSizes = new Dictionary<int, HashSet<int>>();

        for (var g = 1; g < card; g++)
        {
            var sizes = new HashSet<int>();
            for (var i = 0; i < card; i++)
            {
                var targetIdx = (i + g) % card;
                var semitones = (pcs[targetIdx] - pcs[i] + 12) % 12;
                if (semitones == 0) semitones = 12;
                sizes.Add(semitones);
            }
            genericSizes[g] = sizes;
        }

        var isStrictlyProper = true;
        var isProper = true;

        for (var g1 = 1; g1 < card; g1++)
        {
            for (var g2 = g1 + 1; g2 < card; g2++)
            {
                var maxG1 = genericSizes[g1].Max();
                var minG2 = genericSizes[g2].Min();

                if (maxG1 > minG2)
                {
                    isProper = false;
                    isStrictlyProper = false;
                }
                else if (maxG1 == minG2)
                {
                    isStrictlyProper = false;
                }
            }
        }

        if (isStrictlyProper) return RothenbergPropriety.StrictlyProper;
        if (isProper) return RothenbergPropriety.Proper;
        return RothenbergPropriety.Improper;
    }

    /// <summary>
    ///     Computes the Maximal Evenness (ME) discrepancy score (Clough &amp; Douthett).
    ///     Lower score indicates greater evenness (0.0 for perfectly even sets like Whole Tone or Augmented).
    /// </summary>
    public static double GetMaximalEvennessDiscrepancy(this PitchClassSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var card = set.Cardinality.Value;
        if (card == 0 || card == 12) return 0.0;

        var pcs = set.Select(pc => pc.Value).OrderBy(v => v).ToList();
        double totalDiff = 0;

        for (var k = 0; k < card; k++)
        {
            var actualPos = pcs[k];
            var idealPos = (k * 12.0) / card;
            var diff = Math.Abs(actualPos - idealPos);
            // Handle circular wrapping distance
            diff = Math.Min(diff, 12.0 - diff);
            totalDiff += diff * diff;
        }

        return Math.Sqrt(totalDiff / card);
    }

    /// <summary>
    ///     Checks if a scale is Well-Formed (generated by repeatedly stacking a single generator interval mod 12).
    /// </summary>
    public static bool IsWellFormed(this PitchClassSet set, out int generator)
    {
        generator = 0;
        ArgumentNullException.ThrowIfNull(set);
        var card = set.Cardinality.Value;
        if (card <= 1) return true;

        var setPcs = set.Select(pc => pc.Value).ToHashSet();

        // Test candidate generators 1..11
        for (var gen = 1; gen <= 11; gen++)
        {
            if (gen == 6 && card > 2) continue; // Tritone generator only forms 2 notes

            // Try starting from each pitch in the set as root of generator chain
            foreach (var start in setPcs)
            {
                var chain = new HashSet<int>();
                var current = start;
                for (var step = 0; step < card; step++)
                {
                    chain.Add(current);
                    current = (current + gen) % 12;
                }

                if (chain.SetEquals(setPcs))
                {
                    generator = gen;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Counts the number of scale degrees that lack a perfect fifth (+7 semitones) above them within the set.
    /// </summary>
    public static int GetImperfectionCount(this PitchClassSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var pcs = set.Select(pc => pc.Value).ToHashSet();
        var imperfections = 0;

        foreach (var pc in pcs)
        {
            var perfectFifth = (pc + 7) % 12;
            if (!pcs.Contains(perfectFifth))
            {
                imperfections++;
            }
        }

        return imperfections;
    }

    /// <summary>
    ///     Evaluates Zeitler legitimacy criteria for a scale / pitch class set:
    ///     - Contains root (Bit 0 / PitchClass 0)
    ///     - Max gap between consecutive pitches &lt;= 4 semitones
    ///     - Cardinality between 5 and 8
    ///     - No cluster of &gt; 3 consecutive semitones
    /// </summary>
    public static ZeitlerLegitimacy GetZeitlerLegitimacy(this PitchClassSet set, bool requireRootPresent = true)
    {
        ArgumentNullException.ThrowIfNull(set);
        var failedRules = 0;

        var pcs = set.Select(pc => pc.Value).ToHashSet();
        var card = pcs.Count;

        // Rule 1: Has Root (0)
        if (requireRootPresent && !pcs.Contains(0)) failedRules++;

        // Rule 2: Cardinality 5..8
        if (card < 5 || card > 8) failedRules++;

        if (card > 0)
        {
            var sorted = pcs.OrderBy(v => v).ToList();
            var maxGap = 0;
            for (var i = 0; i < card; i++)
            {
                var next = (i + 1) % card;
                var gap = (sorted[next] - sorted[i] + 12) % 12;
                if (gap == 0) gap = 12;
                if (gap > maxGap) maxGap = gap;
            }

            // Rule 3: Max gap <= 4 semitones
            if (maxGap > 4) failedRules++;

            // Rule 4: No cluster > 3 consecutive semitones
            var mask = set.PitchClassMask;
            var doubleMask = mask | (mask << 12);
            var maxCluster = 0;
            var currentCluster = 0;

            for (var bit = 0; bit < 24; bit++)
            {
                if ((doubleMask & (1 << bit)) != 0)
                {
                    currentCluster++;
                    if (currentCluster > maxCluster) maxCluster = currentCluster;
                }
                else
                {
                    currentCluster = 0;
                }
            }

            if (maxCluster > 3) failedRules++;
        }

        if (failedRules == 0) return ZeitlerLegitimacy.LegitimateScale;
        if (failedRules == 1) return ZeitlerLegitimacy.Borderline;
        return ZeitlerLegitimacy.PitchCollection;
    }
}
