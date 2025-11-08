namespace GA.Business.Core.Fretboard.Shapes.Geometry;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

/// <summary>
///     Voice leading space with Riemannian metric
/// </summary>
/// <remarks>
///     Models chord voicings as points in a geometric space where:
///     - Distance = voice leading cost (sum of voice movements)
///     - Geodesics = optimal voice leading paths
///     - Curvature = harmonic tension
///     Based on Dmitri Tymoczko's "A Geometry of Music":
///     - Chords are points in R^n (n = number of voices)
///     - Octave equivalence creates a torus topology
///     - Permutation equivalence creates an orbifold
///     - Voice leading distance = L¹ metric on the orbifold
///     References:
///     - Tymoczko, D. (2011). A Geometry of Music. Oxford University Press.
///     - Callender, C., Quinn, I., & Tymoczko, D. (2008). "Generalized voice-leading spaces"
///     - do Carmo, M. P. (1992). Riemannian Geometry
/// </remarks>
[PublicAPI]
public class VoiceLeadingSpace(
    int voices,
    bool octaveEquivalence = true,
    bool permutationEquivalence = true)
{
    /// <summary>
    ///     Compute voice leading distance between two voicings
    /// </summary>
    /// <param name="from">Source voicing (pitch classes or MIDI notes)</param>
    /// <param name="to">Target voicing</param>
    /// <returns>Minimal voice leading distance</returns>
    /// <remarks>
    ///     Uses L¹ metric: d(v1, v2) = S|v1? - v2?|
    ///     With permutation equivalence, finds optimal voice assignment
    ///     With octave equivalence, considers octave shifts
    /// </remarks>
    public double Distance(double[] from, double[] to)
    {
        if (from.Length != voices || to.Length != voices)
        {
            throw new ArgumentException($"Voicings must have {voices} voices");
        }

        if (!permutationEquivalence)
        {
            return VoiceLeadingCost(from, to);
        }

        // Find optimal permutation (Hungarian algorithm would be better for large n)
        var minCost = double.MaxValue;

        foreach (var perm in Permutations(to))
        {
            var cost = VoiceLeadingCost(from, perm);
            minCost = Math.Min(minCost, cost);
        }

        return minCost;
    }

    /// <summary>
    ///     Compute voice leading cost for a specific voice assignment
    /// </summary>
    private double VoiceLeadingCost(double[] from, double[] to)
    {
        var cost = 0.0;

        for (var i = 0; i < voices; i++)
        {
            var movement = to[i] - from[i];

            if (octaveEquivalence)
            {
                // Find minimal movement considering octave equivalence
                movement = (movement % 12 + 12) % 12;
                if (movement > 6)
                {
                    movement -= 12;
                }
            }

            cost += Math.Abs(movement);
        }

        return cost;
    }

    /// <summary>
    ///     Find geodesic (shortest path) between two voicings
    /// </summary>
    /// <param name="from">Start voicing</param>
    /// <param name="to">End voicing</param>
    /// <param name="steps">Number of intermediate steps</param>
    /// <returns>Sequence of voicings along geodesic</returns>
    public List<double[]> Geodesic(double[] from, double[] to, int steps = 10)
    {
        var path = new List<double[]> { from };

        // Find optimal voice assignment
        var optimalTo = permutationEquivalence
            ? FindOptimalPermutation(from, to)
            : to;

        // Linear interpolation (geodesic in Euclidean space)
        for (var i = 1; i < steps; i++)
        {
            var t = i / (double)steps;
            var intermediate = new double[voices];

            for (var v = 0; v < voices; v++)
            {
                intermediate[v] = from[v] + t * (optimalTo[v] - from[v]);
            }

            path.Add(intermediate);
        }

        path.Add(optimalTo);
        return path;
    }

    /// <summary>
    ///     Compute Riemannian metric tensor at a point
    /// </summary>
    /// <remarks>
    ///     For voice leading space, the metric is typically Euclidean (identity matrix)
    ///     or weighted by voice importance
    /// </remarks>
    public Matrix<double> MetricTensor(double[] point, double[]? weights = null)
    {
        var g = DenseMatrix.CreateIdentity(voices);

        if (weights != null)
        {
            for (var i = 0; i < voices; i++)
            {
                g[i, i] = weights[i];
            }
        }

        return g;
    }

    /// <summary>
    ///     Compute curvature at a point (simplified)
    /// </summary>
    /// <remarks>
    ///     Curvature measures how the space bends
    ///     High curvature = high harmonic tension
    ///     For orbifolds, curvature is concentrated at singular points
    ///     (e.g., augmented triads, diminished seventh chords)
    /// </remarks>
    public double Curvature(double[] point)
    {
        // Simplified: measure distance to nearest singular point
        // Full implementation would compute Riemann curvature tensor

        // Check if point is near a symmetric chord
        var symmetryScore = ComputeSymmetry(point);

        // Higher symmetry = higher curvature
        return symmetryScore;
    }

    /// <summary>
    ///     Compute symmetry score (0 = no symmetry, 1 = perfect symmetry)
    /// </summary>
    private double ComputeSymmetry(double[] voicing)
    {
        var sorted = voicing.OrderBy(v => v).ToArray();
        var intervals = new List<double>();

        for (var i = 1; i < sorted.Length; i++)
        {
            intervals.Add(sorted[i] - sorted[i - 1]);
        }

        // Check if intervals are equal (symmetric division of octave)
        if (intervals.Count == 0)
        {
            return 0;
        }

        var avgInterval = intervals.Average();
        var variance = intervals.Sum(i => Math.Pow(i - avgInterval, 2)) / intervals.Count;

        // Low variance = high symmetry
        return Math.Exp(-variance);
    }

    /// <summary>
    ///     Find optimal permutation of target voicing
    /// </summary>
    private double[] FindOptimalPermutation(double[] from, double[] to)
    {
        var minCost = double.MaxValue;
        double[]? bestPerm = null;

        foreach (var perm in Permutations(to))
        {
            var cost = VoiceLeadingCost(from, perm);
            if (cost < minCost)
            {
                minCost = cost;
                bestPerm = perm;
            }
        }

        return bestPerm ?? to;
    }

    /// <summary>
    ///     Generate all permutations of an array
    /// </summary>
    private IEnumerable<double[]> Permutations(double[] array)
    {
        if (array.Length == 1)
        {
            yield return array;
            yield break;
        }

        for (var i = 0; i < array.Length; i++)
        {
            var element = array[i];
            var remaining = array.Where((_, idx) => idx != i).ToArray();

            foreach (var perm in Permutations(remaining))
            {
                yield return new[] { element }.Concat(perm).ToArray();
            }
        }
    }

    /// <summary>
    ///     Project a point onto the fundamental domain (orbifold)
    /// </summary>
    /// <remarks>
    ///     Reduces voicing to canonical form:
    ///     - Octave equivalence: reduce to [0, 12)
    ///     - Permutation equivalence: sort voices
    ///     - Transposition equivalence: normalize to C
    /// </remarks>
    public double[] ProjectToFundamentalDomain(double[] voicing)
    {
        var result = voicing.ToArray();

        // Octave equivalence
        if (octaveEquivalence)
        {
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (result[i] % 12 + 12) % 12;
            }
        }

        // Permutation equivalence (sort)
        if (permutationEquivalence)
        {
            Array.Sort(result);
        }

        return result;
    }
}

/// <summary>
///     Voice leading analyzer using differential geometry
/// </summary>
[PublicAPI]
public class VoiceLeadingAnalyzer(int voices)
{
    private readonly VoiceLeadingSpace _space = new(voices);

    /// <summary>
    ///     Analyze voice leading between two shapes
    /// </summary>
    public VoiceLeadingInfo Analyze(FretboardShape from, FretboardShape to)
    {
        // Extract voicings (pitch classes of sounding notes)
        var fromVoicing = ExtractVoicing(from);
        var toVoicing = ExtractVoicing(to);

        var distance = _space.Distance(fromVoicing, toVoicing);
        var geodesic = _space.Geodesic(fromVoicing, toVoicing, 5);
        var curvature = _space.Curvature(fromVoicing);

        return new VoiceLeadingInfo
        {
            FromShape = from.Id,
            ToShape = to.Id,
            Distance = distance,
            Curvature = curvature,
            GeodesicLength = geodesic.Count,
            IsSmooth = distance < 5.0 // Arbitrary threshold
        };
    }

    /// <summary>
    ///     Extract voicing from fretboard shape
    /// </summary>
    private double[] ExtractVoicing(FretboardShape shape)
    {
        // Get pitch classes from the pitch class set
        var pitches = shape.PitchClassSet
            .Select(pc => (double)pc.Value)
            .OrderBy(p => p)
            .ToArray();

        return pitches;
    }
}

/// <summary>
///     Information about voice leading between two shapes
/// </summary>
[PublicAPI]
public sealed record VoiceLeadingInfo
{
    public required string FromShape { get; init; }
    public required string ToShape { get; init; }
    public required double Distance { get; init; }
    public required double Curvature { get; init; }
    public required int GeodesicLength { get; init; }
    public required bool IsSmooth { get; init; }

    public override string ToString()
    {
        return $"VoiceLeading[{FromShape} ? {ToShape}, d={Distance:F2}, " +
               $"?={Curvature:F2}, smooth={IsSmooth}]";
    }
}
