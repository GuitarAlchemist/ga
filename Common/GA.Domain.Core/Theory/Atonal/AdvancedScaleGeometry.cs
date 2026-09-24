namespace GA.Domain.Core.Theory.Atonal;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
///     Represents a triad within a pitch class set.
/// </summary>
public record TriadInScale(PitchClass Root, PitchClass Third, PitchClass Fifth, string TriadQuality)
{
    public PitchClassSet ToPitchClassSet() => new([Root, Third, Fifth]);
}

/// <summary>
///     Represents a parsimonious voice-leading connection between two triads.
/// </summary>
public record ParsimoniousTriadConnection(
    TriadInScale FromTriad,
    TriadInScale ToTriad,
    PitchClass MovingVoiceFrom,
    PitchClass MovingVoiceTo,
    int SemitoneShift);

/// <summary>
///     Provides advanced harmonic and geometric calculations:
///     - Parsimonious Voice-Leading Triad Graphs
///     - Interval Matrix & Contradiction / Ambiguity Counts
///     - OPTIC-K 216-Dimensional Embedding Vectors
/// </summary>
public static class AdvancedScaleGeometry
{
    /// <summary>
    ///     Finds all diatonic / tertian triads contained within a pitch class set.
    /// </summary>
    public static IReadOnlyList<TriadInScale> GetTertianTriads(this PitchClassSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var card = set.Cardinality.Value;
        if (card < 3) return Array.Empty<TriadInScale>();

        var pcs = set.Select(pc => pc.Value).ToHashSet();
        var triads = new List<TriadInScale>();

        foreach (var root in pcs)
        {
            // Triad qualities: Major (0, 4, 7), Minor (0, 3, 7), Diminished (0, 3, 6), Augmented (0, 4, 8)
            var maj3 = (root + 4) % 12;
            var min3 = (root + 3) % 12;
            var perf5 = (root + 7) % 12;
            var dim5 = (root + 6) % 12;
            var aug5 = (root + 8) % 12;

            var rPc = PitchClass.FromValue(root);

            if (pcs.Contains(maj3) && pcs.Contains(perf5))
            {
                triads.Add(new TriadInScale(rPc, PitchClass.FromValue(maj3), PitchClass.FromValue(perf5), "Major"));
            }
            if (pcs.Contains(min3) && pcs.Contains(perf5))
            {
                triads.Add(new TriadInScale(rPc, PitchClass.FromValue(min3), PitchClass.FromValue(perf5), "Minor"));
            }
            if (pcs.Contains(min3) && pcs.Contains(dim5))
            {
                triads.Add(new TriadInScale(rPc, PitchClass.FromValue(min3), PitchClass.FromValue(dim5), "Diminished"));
            }
            if (pcs.Contains(maj3) && pcs.Contains(aug5))
            {
                triads.Add(new TriadInScale(rPc, PitchClass.FromValue(maj3), PitchClass.FromValue(aug5), "Augmented"));
            }
        }

        return triads;
    }

    /// <summary>
    ///     Generates parsimonious voice-leading connections (2 common tones, 1 voice moves by 1 or 2 semitones)
    ///     between triads in the set.
    /// </summary>
    public static IReadOnlyList<ParsimoniousTriadConnection> GetParsimoniousTriadConnections(this PitchClassSet set)
    {
        var triads = set.GetTertianTriads();
        var connections = new List<ParsimoniousTriadConnection>();

        for (var i = 0; i < triads.Count; i++)
        {
            for (var j = i + 1; j < triads.Count; j++)
            {
                var t1 = triads[i];
                var t2 = triads[j];

                var pcs1 = new HashSet<int> { t1.Root.Value, t1.Third.Value, t1.Fifth.Value };
                var pcs2 = new HashSet<int> { t2.Root.Value, t2.Third.Value, t2.Fifth.Value };

                var common = pcs1.Intersect(pcs2).ToList();
                if (common.Count == 2)
                {
                    var diff1 = pcs1.Except(common).Single();
                    var diff2 = pcs2.Except(common).Single();

                    var semitoneShift = Math.Abs((diff2 - diff1 + 12) % 12);
                    if (semitoneShift > 6) semitoneShift = 12 - semitoneShift;

                    if (semitoneShift <= 2)
                    {
                        connections.Add(new ParsimoniousTriadConnection(
                            t1, t2, PitchClass.FromValue(diff1), PitchClass.FromValue(diff2), semitoneShift));
                    }
                }
            }
        }

        return connections;
    }

    /// <summary>
    ///     Computes the 216-dimensional OPTIC-K feature embedding vector for a pitch class set:
    ///     12 pitch-class presence indicators x 6 interval class counts x 3 symmetry indicators = 216 dims.
    /// </summary>
    public static double[] GetOpticK216Embedding(this PitchClassSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var embedding = new double[216];
        var pcs = set.Select(pc => pc.Value).ToHashSet();
        var icv = set.IntervalClassVector;

        // Features 0..11: Pitch Class Presence
        for (var pc = 0; pc < 12; pc++)
        {
            if (pcs.Contains(pc)) embedding[pc] = 1.0;
        }

        // Features 12..17: Interval Class Vector Counts
        for (var ic = 1; ic <= 6; ic++)
        {
            embedding[11 + ic] = icv[IntervalClass.FromValue(ic)];
        }

        // Features 18..20: Symmetries
        var isPalindromic = set.Equals(set.Id.Inverse.ToPitchClassSet());
        embedding[18] = isPalindromic ? 1.0 : 0.0;
        embedding[19] = set.IsMonomodal ? 1.0 : 0.0;
        embedding[20] = set.HasMyhillProperty() ? 1.0 : 0.0;

        // Fill remaining feature channels using tensor products (PitchClass x ICV)
        var idx = 21;
        for (var pc = 0; pc < 12 && idx < 216; pc++)
        {
            for (var ic = 1; ic <= 6 && idx < 216; ic++)
            {
                var pcPresent = pcs.Contains(pc) ? 1.0 : 0.0;
                var icCount = icv[IntervalClass.FromValue(ic)];
                embedding[idx++] = pcPresent * icCount;
            }
        }

        return embedding;
    }

    /// <summary>
    ///     Computes interval contradiction and ambiguity counts for a scale.
    ///     A contradiction occurs when a smaller generic interval has a larger specific size than a larger generic interval.
    /// </summary>
    public static (int Contradictions, int Ambiguities) GetIntervalMatrixDiagnostics(this PitchClassSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var card = set.Cardinality.Value;
        if (card < 2) return (0, 0);

        var pcs = set.Select(pc => pc.Value).OrderBy(v => v).ToList();
        var genericSizes = new Dictionary<int, List<int>>();

        for (var g = 1; g < card; g++)
        {
            var sizes = new List<int>();
            for (var i = 0; i < card; i++)
            {
                var targetIdx = (i + g) % card;
                var semitones = (pcs[targetIdx] - pcs[i] + 12) % 12;
                if (semitones == 0) semitones = 12;
                sizes.Add(semitones);
            }
            genericSizes[g] = sizes;
        }

        var contradictions = 0;
        var ambiguities = 0;

        for (var g1 = 1; g1 < card; g1++)
        {
            for (var g2 = g1 + 1; g2 < card; g2++)
            {
                foreach (var s1 in genericSizes[g1])
                {
                    foreach (var s2 in genericSizes[g2])
                    {
                        if (s1 > s2) contradictions++;
                        if (s1 == s2) ambiguities++;
                    }
                }
            }
        }

        return (contradictions, ambiguities);
    }
}
