namespace GA.Domain.Services.Atonal;

using GA.Domain.Core.Theory.Atonal;
using JetBrains.Annotations;

/// <summary>
/// Provides a coordinate-based voice-leading space for comparing arbitrary pitch collections.
/// Implements OPTIC equivalences (Octave, Permutation, Transposition, Inversion).
/// </summary>
[PublicAPI]
public sealed class VoiceLeadingSpace(int voices, bool octaveEquivalence, bool permutationEquivalence, bool transpositionEquivalence, bool inversionEquivalence)
{
    private readonly VoiceLeadingOptions _options = new()
    {
        Voices = voices,
        OctaveEquivalence = octaveEquivalence,
        PermutationEquivalence = permutationEquivalence,
        TranspositionEquivalence = transpositionEquivalence,
        InversionEquivalence = inversionEquivalence
    };

    public double Distance(double[] a, double[] b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Voice count mismatch");
        if (a.Length == 0) return 0.0;

        return ComputeMinDistance(a, b, _options);
    }

    private double ComputeMinDistance(double[] a, double[] b, VoiceLeadingOptions options)
    {
        var minDistance = double.MaxValue;

        // Try original and inversion
        minDistance = Math.Min(minDistance, ComputeTranspositionInvariantDistance(a, b, options));

        if (options.InversionEquivalence)
        {
            var invB = b.Select(x => -x).ToArray();
            minDistance = Math.Min(minDistance, ComputeTranspositionInvariantDistance(a, invB, options));
        }

        return minDistance;
    }

    private double ComputeTranspositionInvariantDistance(double[] a, double[] b, VoiceLeadingOptions options)
    {
        if (!options.TranspositionEquivalence)
        {
            return ComputePermutationInvariantDistance(a, b, options);
        }

        var minDistance = double.MaxValue;
        // For discrete pitch classes, try all semitones.
        for (var c = 0; c < 12; c++)
        {
            var shiftedB = b.Select(x => x + c).ToArray();
            minDistance = Math.Min(minDistance, ComputePermutationInvariantDistance(a, shiftedB, options));
        }
        return minDistance;
    }

    private double ComputePermutationInvariantDistance(double[] a, double[] b, VoiceLeadingOptions options)
    {
        if (!options.PermutationEquivalence)
        {
            return ComputeOctaveInvariantDistance(a, b, options);
        }

        // Optimal permutation for circular distance between sorted sets is one of the cyclic shifts.
        var sortedA = a.OrderBy(x => x).ToArray();
        var sortedB = b.OrderBy(x => x).ToArray();
        var n = a.Length;

        if (!options.OctaveEquivalence)
        {
            return ComputeOctaveInvariantDistance(sortedA, sortedB, options);
        }

        var minDistance = double.MaxValue;
        for (var k = 0; k < n; k++)
        {
            var shiftedB = new double[n];
            for (var i = 0; i < n; i++)
            {
                shiftedB[i] = sortedB[(i + k) % n];
            }
            minDistance = Math.Min(minDistance, ComputeOctaveInvariantDistance(sortedA, shiftedB, options));
        }
        return minDistance;
    }

    private double ComputeOctaveInvariantDistance(double[] a, double[] b, VoiceLeadingOptions options)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = Math.Abs(a[i] - b[i]);
            if (options.OctaveEquivalence)
            {
                // Circular distance mod 12
                var modDiff = diff % 12;
                if (modDiff > 6) modDiff = 12 - modDiff;
                sum += modDiff;
            }
            else
            {
                sum += diff;
            }
        }
        return sum;
    }
}
