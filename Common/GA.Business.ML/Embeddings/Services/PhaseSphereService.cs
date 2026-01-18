namespace GA.Business.ML.Embeddings.Services;

using System;
using System.Linq;
using System.Numerics;

/// <summary>
/// Implements the Phase Sphere framework for spectral geometry of pitch-class sets.
/// 
/// <para>
/// The Phase Sphere projects pitch-class sets onto a high-dimensional unit sphere
/// via normalized Fourier spectra. This enables:
/// </para>
/// <list type="bullet">
///   <item><description>Continuous harmonic distance measurement (spectral distance)</description></item>
///   <item><description>Transposition as rigid rotation</description></item>
///   <item><description>Voice-leading as geodesics on the sphere</description></item>
///   <item><description>Z-relation detection via phase comparison</description></item>
/// </list>
/// </summary>
/// <remarks>
/// Reference: Amiot, E. (2016). "Music Through Fourier Space"
/// </remarks>
public class PhaseSphereService
{
    private const int N = 12; // Chromatic cardinality
    private const int MaxK = 6; // Meaningful DFT components (k > 6 are conjugates)

    /// <summary>
    /// Computes the spectral vector S(X) for a pitch-class set.
    /// Returns complex DFT coefficients for k=1..6.
    /// </summary>
    /// <param name="pitchClasses">Array of pitch classes [0-11].</param>
    /// <returns>Complex spectral vector with 6 components.</returns>

    public Complex[] ComputeSpectralVector(int[] pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);
        
        // Build chroma vector (binary encoding)
        var chroma = new double[N];
        foreach (var pc in pitchClasses)
        {
            if (pc >= 0 && pc < N)
                chroma[pc] = 1.0;
        }

        return ComputeSpectralVector(chroma);
    }

    /// <summary>
    /// Computes the spectral vector S(X) for a weighted chroma vector.
    /// Supports "Salient Chroma" (prominence, doubling, bass weight).
    /// </summary>
    /// <param name="weightedChroma">Array of 12 double weights.</param>
    /// <returns>Complex spectral vector with 6 components.</returns>
    public Complex[] ComputeSpectralVector(double[] weightedChroma)
    {
        ArgumentNullException.ThrowIfNull(weightedChroma);
        if (weightedChroma.Length != N)
            throw new ArgumentException($"Chroma must have {N} elements", nameof(weightedChroma));

        // Compute DFT coefficients for k=1..6
        var spectral = new Complex[MaxK];
        
        for (var k = 1; k <= MaxK; k++)
        {
            var real = 0.0;
            var imag = 0.0;
            
            for (var n = 0; n < N; n++)
            {
                var angle = -2.0 * Math.PI * k * n / N;
                real += weightedChroma[n] * Math.Cos(angle);
                imag += weightedChroma[n] * Math.Sin(angle);
            }
            
            spectral[k - 1] = new(real, imag);
        }

        return spectral;
    }

    /// <summary>
    /// Computes weighted spectral vector using note salience (doubling, bass/melody emphasis).
    /// </summary>
    /// <param name="midiNotes">Array of MIDI note numbers.</param>
    /// <returns>Weighted complex spectral vector.</returns>
    public Complex[] ComputeWeightedSpectralVector(int[] midiNotes)
    {
        ArgumentNullException.ThrowIfNull(midiNotes);
        if (midiNotes.Length == 0)
            return new Complex[MaxK];

        var chroma = new double[N];
        var minNote = midiNotes.Min();
        var maxNote = midiNotes.Max();

        foreach (var note in midiNotes)
        {
            var pc = note % N;
            var weight = 1.0;
            
            // Bass emphasis (+0.5)
            if (note == minNote) weight += 0.5;
            // Melody emphasis (+0.5)
            if (note == maxNote) weight += 0.5;
            
            chroma[pc] += weight;
        }

        // Normalize chroma
        var maxChroma = chroma.Max();
        if (maxChroma > 0)
        {
            for (var i = 0; i < N; i++)
                chroma[i] /= maxChroma;
        }

        // Compute DFT
        var spectral = new Complex[MaxK];
        
        for (var k = 1; k <= MaxK; k++)
        {
            var real = 0.0;
            var imag = 0.0;
            
            for (var n = 0; n < N; n++)
            {
                var angle = -2.0 * Math.PI * k * n / N;
                real += chroma[n] * Math.Cos(angle);
                imag += chroma[n] * Math.Sin(angle);
            }
            
            spectral[k - 1] = new(real, imag);
        }

        return spectral;
    }

    /// <summary>
    /// Normalizes spectral vector to unit sphere (Phase Sphere projection).
    /// </summary>
    /// <param name="spectralVector">Unnormalized spectral vector.</param>
    /// <returns>Unit-length spectral vector.</returns>
    public Complex[] NormalizeToSphere(Complex[] spectralVector)
    {
        ArgumentNullException.ThrowIfNull(spectralVector);
        
        var norm = Math.Sqrt(spectralVector.Sum(c => c.Magnitude * c.Magnitude));
        
        if (norm < 1e-10)
            return new Complex[spectralVector.Length];

        return spectralVector.Select(c => c / norm).ToArray();
    }

    /// <summary>
    /// Computes spectral distance (geodesic angle) between two pitch-class sets.
    /// </summary>
    /// <param name="setA">First pitch-class set.</param>
    /// <param name="setB">Second pitch-class set.</param>
    /// <returns>Angle in radians [0, π].</returns>
    public double SpectralDistance(int[] setA, int[] setB)
    {
        var specA = NormalizeToSphere(ComputeSpectralVector(setA));
        var specB = NormalizeToSphere(ComputeSpectralVector(setB));

        return SpectralDistanceFromVectors(specA, specB);
    }

    /// <summary>
    /// Computes spectral distance from pre-computed normalized vectors.
    /// </summary>
    public double SpectralDistanceFromVectors(Complex[] normalizedA, Complex[] normalizedB)
    {
        if (normalizedA.Length != normalizedB.Length)
            throw new ArgumentException("Spectral vectors must have same length");

        // Hermitian inner product: Re(A · conj(B))
        var dotProduct = 0.0;
        for (var i = 0; i < normalizedA.Length; i++)
        {
            dotProduct += (normalizedA[i] * Complex.Conjugate(normalizedB[i])).Real;
        }

        // Clamp for numerical stability
        dotProduct = Math.Clamp(dotProduct, -1.0, 1.0);

        return Math.Acos(dotProduct);
    }

    /// <summary>
    /// Detects if two sets are Z-related (same magnitudes, different phases).
    /// </summary>
    /// <param name="setA">First pitch-class set.</param>
    /// <param name="setB">Second pitch-class set.</param>
    /// <param name="tolerance">Magnitude comparison tolerance.</param>
    /// <returns>True if Z-related.</returns>
    public bool AreZRelated(int[] setA, int[] setB, double tolerance = 1e-6)
    {
        if (setA.Length != setB.Length)
            return false;

        var specA = ComputeSpectralVector(setA);
        var specB = ComputeSpectralVector(setB);

        // Check if magnitudes match
        for (var k = 0; k < MaxK; k++)
        {
            if (Math.Abs(specA[k].Magnitude - specB[k].Magnitude) > tolerance)
                return false;
        }

        // Check if phases differ (at least one phase ≠)
        var phasesMatch = true;
        for (var k = 0; k < MaxK; k++)
        {
            if (specA[k].Magnitude > tolerance && specB[k].Magnitude > tolerance)
            {
                var phaseA = specA[k].Phase;
                var phaseB = specB[k].Phase;
                var phaseDiff = Math.Abs(NormalizeAngle(phaseA - phaseB));
                
                if (phaseDiff > tolerance)
                {
                    phasesMatch = false;
                    break;
                }
            }
        }

        // Z-related = same magnitudes, different phases
        return !phasesMatch;
    }

    /// <summary>
    /// Computes the phase angle difference for Z-related sets.
    /// Returns the average absolute phase difference across components.
    /// </summary>
    public double ZRelatedPhaseAngle(int[] setA, int[] setB)
    {
        var specA = ComputeSpectralVector(setA);
        var specB = ComputeSpectralVector(setB);

        var totalDiff = 0.0;
        var count = 0;

        for (var k = 0; k < MaxK; k++)
        {
            if (specA[k].Magnitude > 0.1 && specB[k].Magnitude > 0.1)
            {
                totalDiff += Math.Abs(NormalizeAngle(specA[k].Phase - specB[k].Phase));
                count++;
            }
        }

        return count > 0 ? totalDiff / count : 0.0;
    }

    /// <summary>
    /// Extracts magnitudes and phases for embedding.
    /// </summary>
    /// <param name="pitchClasses">Pitch-class set.</param>
    /// <returns>Tuple of (magnitudes[6], phases[6]).</returns>
    public (double[] Magnitudes, double[] Phases) ExtractMagnitudesAndPhases(int[] pitchClasses)
    {
        var spectral = ComputeSpectralVector(pitchClasses);
        
        var magnitudes = spectral.Select(c => c.Magnitude).ToArray();
        var phases = spectral.Select(c => (c.Phase + Math.PI) / (2.0 * Math.PI)).ToArray(); // Normalize to [0,1]

        return (magnitudes, phases);
    }

    /// <summary>
    /// Computes phase coherence (how aligned phases are).
    /// High coherence = phases clustered; Low = dispersed.
    /// </summary>
    public double ComputePhaseCoherence(int[] pitchClasses)
    {
        var spectral = ComputeSpectralVector(pitchClasses);
        
        // Compute mean phasor (weighted by magnitude)
        var meanReal = 0.0;
        var meanImag = 0.0;
        var totalMag = 0.0;

        foreach (var c in spectral)
        {
            var mag = c.Magnitude;
            if (mag > 0)
            {
                meanReal += Math.Cos(c.Phase) * mag;
                meanImag += Math.Sin(c.Phase) * mag;
                totalMag += mag;
            }
        }

        if (totalMag < 1e-10)
            return 0.0;

        meanReal /= totalMag;
        meanImag /= totalMag;

        // Coherence = magnitude of mean phasor
        return Math.Sqrt(meanReal * meanReal + meanImag * meanImag);
    }

    /// <summary>
    /// Computes projection onto specific cycle axis (e.g., fifths, tritones).
    /// </summary>
    /// <param name="pitchClasses">Pitch-class set.</param>
    /// <param name="k">Cycle index (1=semitones, 5=fifths, 6=tritones).</param>
    /// <returns>Normalized magnitude projection [0,1].</returns>
    public double CycleProjection(int[] pitchClasses, int k)
    {
        if (k < 1 || k > MaxK)
            throw new ArgumentOutOfRangeException(nameof(k), "k must be 1-6");

        var spectral = ComputeSpectralVector(pitchClasses);
        var norm = spectral.Select(c => c.Magnitude).ToArray();
        var total = norm.Sum();

        return total > 0 ? norm[k - 1] / total : 0.0;
    }

    /// <summary>
    /// Identifies the dominant cycle (which periodicity dominates).
    /// </summary>
    /// <param name="pitchClasses">Pitch-class set.</param>
    /// <returns>Dominant cycle k (1-6) and its normalized strength.</returns>
    public (int DominantK, double Strength) IdentifyDominantCycle(int[] pitchClasses)
    {
        var spectral = ComputeSpectralVector(pitchClasses);
        var mags = spectral.Select((c, i) => (Magnitude: c.Magnitude, K: i + 1)).ToArray();

        var dominant = mags.OrderByDescending(m => m.Magnitude).First();
        var total = mags.Sum(m => m.Magnitude);

        return (dominant.K, total > 0 ? dominant.Magnitude / total : 0.0);
    }

    /// <summary>
    /// Computes the spectral entropy (H) of a pitch-class set.
    /// H = -sum(p_k * log(p_k)) where p_k is normalized power of component k.
    /// Measures harmonic dimensionality: 0 = pure cycle, 1 = chromatic chaos.
    /// </summary>
    public double ComputeSpectralEntropy(int[] pitchClasses)
    {
        var spectral = ComputeSpectralVector(pitchClasses);
        var powers = spectral.Select(c => c.Magnitude * c.Magnitude).ToArray();
        var totalPower = powers.Sum();

        if (totalPower < 1e-10) return 0.0; // Empty set has 0 entropy (or could be undefined)

        var entropy = 0.0;
        foreach (var p in powers)
        {
            if (p > 1e-10)
            {
                var prob = p / totalPower;
                entropy -= prob * Math.Log(prob);
            }
        }

        // Normalize entropy to [0, 1] range by dividing by log(6) (max entropy for 6 components)
        return entropy / Math.Log(MaxK);
    }

    /// <summary>
    /// Computes the Spectral Barycenter of a sequence of pitch-class sets.
    /// This represents the "harmonic center of gravity" or computed key center.
    /// </summary>
    /// <param name="progression">List of pitch-class sets in the progression.</param>
    /// <returns>Normalized spectral vector representing the barycenter.</returns>
    public Complex[] ComputeSpectralBarycenter(List<int[]> progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        if (progression.Count == 0) return new Complex[MaxK];

        var sumVector = new Complex[MaxK];

        // Sum normalized vectors
        foreach (var pcs in progression)
        {
            var vec = NormalizeToSphere(ComputeSpectralVector(pcs));
            for (var k = 0; k < MaxK; k++)
            {
                sumVector[k] += vec[k];
            }
        }

        // Average
        for (var k = 0; k < MaxK; k++)
        {
            sumVector[k] /= progression.Count;
        }

        // Renormalize to sphere
        return NormalizeToSphere(sumVector);
    }

    /// <summary>
    /// Computes the Spectral Velocity profile of a progression.
    /// v_i = distance(S_i, S_{i+1}).
    /// Represents harmonic motion, turbulence, or voice-leading cost.
    /// </summary>
    public double[] ComputeSpectralVelocity(List<int[]> progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        if (progression.Count < 2) return Array.Empty<double>();

        var velocities = new double[progression.Count - 1];
        var vecs = progression.Select(p => NormalizeToSphere(ComputeSpectralVector(p))).ToList();

        for (var i = 0; i < vecs.Count - 1; i++)
        {
            velocities[i] = SpectralDistanceFromVectors(vecs[i], vecs[i + 1]);
        }

        return velocities;
    }

    /// <summary>
    /// Computes the Spectral Curvature (Perceptual Volatility).
    /// Modeled as a function of entropy and cycle dominance.
    /// High curvature = high ambiguity/volatility (chromatic).
    /// Low curvature = flat/stable (diatonic).
    /// </summary>
    public double ComputeSpectralCurvature(int[] pitchClasses)
    {
        // Simple curvature model based on entropy
        // High entropy = High curvature (volatile)
        // We can refine this to look specifically at k=1 vs k=5 balance
        
        var entropy = ComputeSpectralEntropy(pitchClasses);
        var k5 = CycleProjection(pitchClasses, 5); // Fifths axis (tonal stability)
        
        // Curvature increases with entropy but decreases with strong tonal or whole-tone structure
        // This is a heuristic approximation of the manifold curvature
        return Math.Clamp(entropy * (1.0 - k5), 0.0, 1.0);
    }

    /// <summary>
    /// Transposes a spectral vector by t semitones (Rotation).
    /// Uses theorem: F_k(X+t) = F_k(X) * e^(-i * 2 * pi * k * t / 12)
    /// </summary>
    public Complex[] RotateSpectralVector(Complex[] vector, int semitones)
    {
        var rotated = new Complex[vector.Length];
        
        for (var k = 0; k < vector.Length; k++)
        {
            // vector index k corresponds to Fourier component k+1
            var componentK = k + 1;
            var angle = -2.0 * Math.PI * componentK * semitones / 12.0;
            var rotation = new Complex(Math.Cos(angle), Math.Sin(angle));
            rotated[k] = vector[k] * rotation;
        }

        return rotated;
    }

    /// <summary>
    /// Computes Relative Phase (phi_k) with respect to a reference vector (e.g., Barycenter).
    /// Essential for functional harmony (distance from tonic).
    /// </summary>
    public double[] ComputeRelativePhases(Complex[] target, Complex[] reference)
    {
        if (target.Length != reference.Length)
            throw new ArgumentException("Vectors must be same length");

        var phases = new double[target.Length];
        for (var i = 0; i < target.Length; i++)
        {
            // Relative phase is the angle of (Target * conj(Reference))
            // This is effectively phase(Target) - phase(Reference)
            var relComplex = target[i] * Complex.Conjugate(reference[i]);
            phases[i] = relComplex.Phase; // Result in [-pi, pi]
        }
        return phases;
    }

    /// <summary>
    /// Normalizes angle to [-π, π].
    /// </summary>
    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI) angle -= 2 * Math.PI;
        while (angle < -Math.PI) angle += 2 * Math.PI;
        return angle;
    }
}
