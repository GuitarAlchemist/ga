namespace GA.Business.ML.Wavelets;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Service for Discrete Wavelet Transform (DWT) analysis of musical signals.
/// Supports multi-level decomposition using Haar and Daubechies-4 wavelets.
/// </summary>
public class WaveletTransformService
{
    public enum WaveletFamily
    {
        Haar,
        Daubechies4
    }

    /// <summary>
    /// Performs multi-level DWT decomposition on a signal.
    /// </summary>
    /// <param name="signal">Input scalar time series (e.g., entropy, velocity)</param>
    /// <param name="family">Wavelet family (default: db4)</param>
    /// <param name="levels">Decomposition levels (null = adaptive)</param>
    public WaveletDecomposition Decompose(
        double[] signal,
        WaveletFamily family = WaveletFamily.Daubechies4,
        int? levels = null)
    {
        if (signal == null || signal.Length == 0)
            throw new ArgumentException("Signal cannot be empty", nameof(signal));

        int L = levels ?? ComputeAdaptiveLevels(signal.Length);
        var (h, g) = GetFilters(family);

        var currentApx = signal;
        var detailCoefficients = new double[L][];

        for (int l = 0; l < L; l++)
        {
            var (nextApx, nextDet) = Step(currentApx, h, g);
            currentApx = nextApx;
            detailCoefficients[l] = nextDet;
        }

        return new(currentApx, detailCoefficients, L, family);
    }

    /// <summary>
    /// Computes adaptive decomposition levels based on signal length.
    /// Formula: L = min(3, floor(log2(T)) - 2)
    /// </summary>
    public static int ComputeAdaptiveLevels(int signalLength)
    {
        if (signalLength < 4) return 1;
        int maxLevel = (int)Math.Floor(Math.Log2(signalLength)) - 2;
        return Math.Clamp(maxLevel, 1, 3);
    }

    /// <summary>
    /// Extracts a unified feature vector from a wavelet decomposition.
    /// Features: [Mean, StdDev, Energy, Entropy] for Apx and each Detail level.
    /// </summary>
    public double[] ExtractFeatures(WaveletDecomposition decomp)
    {
        var features = new List<double>();

        // 1. Approximation Features
        features.AddRange(ComputeStats(decomp.ApproximationCoefficients));

        // 2. Detail Features (Level 1 to L)
        foreach (var detail in decomp.DetailCoefficients)
        {
            features.AddRange(ComputeStats(detail));
        }

        // 3. Zero-padding to maintain consistent dimension (assuming max level 3)
        // Apx + 3 detail levels = 4 * 4 = 16 features
        while (features.Count < 16)
        {
            features.Add(0.0);
        }

        return features.ToArray();
    }

    private (double[] Approximation, double[] Detail) Step(double[] signal, double[] h, double[] g)
    {
        int n = signal.Length;
        int filterLen = h.Length;
        int half = (n + 1) / 2; // Handle odd lengths by padding conceptually or using periodic boundary

        var apx = new double[half];
        var det = new double[half];

        for (int i = 0; i < half; i++)
        {
            double a = 0;
            double d = 0;

            for (int j = 0; j < filterLen; j++)
            {
                // Periodic boundary extension
                int idx = (2 * i + j) % n;
                a += signal[idx] * h[j];
                d += signal[idx] * g[j];
            }

            apx[i] = a;
            det[i] = d;
        }

        return (apx, det);
    }

    private (double[] h, double[] g) GetFilters(WaveletFamily family)
    {
        return family switch
        {
            WaveletFamily.Haar => (
                new[] { 0.7071067811865476, 0.7071067811865476 },
                new[] { 0.7071067811865476, -0.7071067811865476 }
            ),
            WaveletFamily.Daubechies4 => (
                new[] { 0.4829629131445341, 0.8365163037378077, 0.2241438680420134, -0.1294095225512603 },
                new[] { -0.1294095225512603, -0.2241438680420134, 0.8365163037378077, -0.4829629131445341 }
            ),
            _ => throw new NotSupportedException()
        };
    }

    private double[] ComputeStats(double[] data)
    {
        if (data.Length == 0) return new[] { 0.0, 0.0, 0.0, 0.0 };

        double mean = data.Average();
        double sumSqDiff = data.Sum(x => Math.Pow(x - mean, 2));
        double std = Math.Sqrt(sumSqDiff / data.Length);
        double energy = data.Sum(x => x * x);
        
        // Normalized Shannon Entropy of coefficients
        double totalAbs = data.Sum(x => Math.Abs(x));
        double entropy = 0;
        if (totalAbs > 0)
        {
            foreach (var x in data)
            {
                double p = Math.Abs(x) / totalAbs;
                if (p > 0) entropy -= p * Math.Log2(p);
            }
        }

        return new[] { mean, std, energy, entropy };
    }
}

public record WaveletDecomposition(
    double[] ApproximationCoefficients,
    double[][] DetailCoefficients,
    int Levels,
    WaveletTransformService.WaveletFamily Wavelet
);
