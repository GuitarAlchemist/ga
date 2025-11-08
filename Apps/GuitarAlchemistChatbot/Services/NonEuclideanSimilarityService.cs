namespace GuitarAlchemistChatbot.Services;

using System.Numerics;

/// <summary>
///     Provides multi-space similarity scoring inspired by TARS' non-Euclidean vector store.
/// </summary>
public class NonEuclideanSimilarityService(ILogger<NonEuclideanSimilarityService> logger)
{
    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var fa = a[i];
            var fb = b[i];
            dot += fa * fb;
            magA += fa * fa;
            magB += fb * fb;
        }

        if (magA < 1e-9 || magB < 1e-9)
        {
            return 0.0;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private static double Projective(float[] a, float[] b)
    {
        var normA = Math.Sqrt(a.Sum(x => x * x));
        var normB = Math.Sqrt(b.Sum(x => x * x));
        if (normA < 1e-9 || normB < 1e-9)
        {
            return 0.0;
        }

        var unitA = a.Select(x => x / normA).ToArray();
        var unitB = b.Select(x => x / normB).ToArray();
        return Math.Abs(Cosine(unitA.Select(Convert.ToSingle).ToArray(), unitB.Select(Convert.ToSingle).ToArray()));
    }

    private static double Hyperbolic(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return 0.0;
        }

        static double Clamp(double value)
        {
            return Math.Clamp(value, -0.99, 0.99);
        }

        var hyperDistance = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            hyperDistance += diff * diff;
        }

        var normA = Math.Sqrt(a.Sum(x => x * x));
        var normB = Math.Sqrt(b.Sum(x => x * x));

        var r1 = Clamp(normA);
        var r2 = Clamp(normB);

        var numerator = 2.0 * hyperDistance;
        var denominator = (1.0 - r1 * r1) * (1.0 - r2 * r2);
        if (denominator < 1e-9)
        {
            return 0.0;
        }

        var argument = 1.0 + numerator / denominator;
        var distance = Math.Log(argument + Math.Sqrt(argument * argument - 1.0));
        return 1.0 / (1.0 + distance);
    }

    private static double Wavelet(float[] a, float[] b)
    {
        if (a.Length < 3 || b.Length < 3)
        {
            return Cosine(a, b);
        }

        static double[] WindowedAverage(float[] source, int window)
        {
            var result = new double[source.Length - window + 1];
            for (var i = 0; i < result.Length; i++)
            {
                double sum = 0;
                for (var j = 0; j < window; j++)
                {
                    sum += source[i + j];
                }

                result[i] = sum / window;
            }

            return result;
        }

        var window = Math.Max(3, a.Length / 8);
        var avgA = WindowedAverage(a, window);
        var avgB = WindowedAverage(b, window);
        return Cosine(avgA.Select(Convert.ToSingle).ToArray(), avgB.Select(Convert.ToSingle).ToArray());
    }

    private static double Minkowski(float[] a, float[] b)
    {
        if (a.Length < 2 || b.Length < 2)
        {
            return Cosine(a, b);
        }

        var spatialLength = a.Length - 1;
        var spatialA = new double[spatialLength];
        var spatialB = new double[spatialLength];
        Array.Copy(a, spatialA, spatialLength);
        Array.Copy(b, spatialB, spatialLength);

        var timeA = a[^1];
        var timeB = b[^1];

        var spatialDiff = 0.0;
        for (var i = 0; i < spatialLength; i++)
        {
            var diff = spatialA[i] - spatialB[i];
            spatialDiff += diff * diff;
        }

        var timeDiff = (timeA - timeB) * (timeA - timeB);
        var interval = Math.Sqrt(Math.Abs(spatialDiff - timeDiff));
        return 1.0 / (1.0 + interval);
    }

    private static double Phase(float[] a, float[] b)
    {
        var length = a.Length;
        var complexA = new Complex[length];
        var complexB = new Complex[length];
        for (var i = 0; i < length; i++)
        {
            complexA[i] = new Complex(a[i], 0);
            complexB[i] = new Complex(b[i], 0);
        }

        // Simplified phase comparison using angle difference
        var phaseDiff = 0.0;
        for (var i = 0; i < length; i++)
        {
            var phaseA = complexA[i].Phase;
            var phaseB = complexB[i].Phase;
            phaseDiff += Math.Abs(phaseA - phaseB);
        }

        var avgDiff = phaseDiff / length;
        return 1.0 / (1.0 + avgDiff);
    }

    public NonEuclideanSimilarityResult Compute(float[] query, float[] candidate)
    {
        if (query.Length != candidate.Length)
        {
            throw new ArgumentException("Vectors must share the same dimensionality", nameof(candidate));
        }

        var raw = Cosine(query, candidate);
        var hyperbolic = Hyperbolic(query, candidate);
        var projective = Projective(query, candidate);
        var wavelet = Wavelet(query, candidate);
        var minkowski = Minkowski(query, candidate);
        var phase = Phase(query, candidate);

        var weighted =
            raw * 0.35 +
            hyperbolic * 0.2 +
            projective * 0.15 +
            wavelet * 0.1 +
            minkowski * 0.1 +
            phase * 0.1;

        logger.LogDebug(
            "Non-Euclidean similarity computed. Raw={Raw:F4}, Hyperbolic={Hyperbolic:F4}, Projective={Projective:F4}, Wavelet={Wavelet:F4}, Minkowski={Minkowski:F4}, Phase={Phase:F4}, Aggregated={Aggregated:F4}",
            raw, hyperbolic, projective, wavelet, minkowski, phase, weighted);

        return new NonEuclideanSimilarityResult(weighted, raw, hyperbolic, projective, wavelet, minkowski, phase);
    }
}

public readonly record struct NonEuclideanSimilarityResult(
    double Aggregated,
    double Raw,
    double Hyperbolic,
    double Projective,
    double Wavelet,
    double Minkowski,
    double Phase);
