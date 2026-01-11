namespace GA.Business.Core.Fretboard.Shapes.InformationTheory;

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

/// <summary>
///     Information-theoretic metrics for analyzing musical structures
/// </summary>
/// <remarks>
///     Information theory quantifies uncertainty, surprise, and information content.
///     Key concepts:
///     - Shannon Entropy: H(X) = -S p(x) log2 p(x) - measures uncertainty/randomness
///     - Mutual Information: I(X;Y) = H(X) + H(Y) - H(X,Y) - measures dependence
///     - KL Divergence: D_KL(P||Q) = S p(x) log2(p(x)/q(x)) - measures distribution difference
///     - Conditional Entropy: H(X|Y) = H(X,Y) - H(Y) - uncertainty of X given Y
///     Musical applications:
///     - Measure predictability of chord progressions
///     - Quantify harmonic complexity
///     - Detect patterns and redundancy
///     - Optimize practice sequences (maximize information gain)
///     - Compare different harmonic systems
///     References:
///     - Shannon, C. E. (1948). "A Mathematical Theory of Communication"
///     - Cover, T. M., & Thomas, J. A. (2006). Elements of Information Theory
///     - Temperley, D. (2007). Music and Probability
/// </remarks>
[PublicAPI]
public static class EntropyMetrics
{
    private const double _log2 = 0.693147180559945309417232121458; // ln(2)

    /// <summary>
    ///     Compute Shannon entropy H(X) = -S p(x) log2 p(x)
    /// </summary>
    /// <param name="probabilities">Probability distribution (must sum to 1)</param>
    /// <returns>Entropy in bits</returns>
    /// <remarks>
    ///     Entropy measures the average information content or uncertainty:
    ///     - H = 0: Completely predictable (one outcome has p=1)
    ///     - H = log2(n): Maximum uncertainty (uniform distribution)
    ///     For music:
    ///     - Low entropy: Predictable, repetitive progressions
    ///     - High entropy: Surprising, complex progressions
    /// </remarks>
    public static double ShannonEntropy(IEnumerable<double> probabilities)
    {
        var probs = probabilities.Where(p => p > 0).ToArray();

        if (probs.Length == 0)
        {
            return 0;
        }

        // Validate probabilities sum to ~1
        var sum = probs.Sum();
        if (Math.Abs(sum - 1.0) > 0.01)
        {
            // Normalize
            probs = [.. probs.Select(p => p / sum)];
        }

        return -probs.Sum(p => p * Math.Log(p) / _log2);
    }

    /// <summary>
    ///     Compute Shannon entropy from frequency counts
    /// </summary>
    public static double ShannonEntropyFromCounts(IEnumerable<int> counts)
    {
        var countArray = counts.Where(c => c > 0).ToArray();
        if (countArray.Length == 0)
        {
            return 0;
        }

        var total = (double)countArray.Sum();
        var probabilities = countArray.Select(c => c / total);

        return ShannonEntropy(probabilities);
    }

    /// <summary>
    ///     Compute joint entropy H(X,Y) = -S p(x,y) log2 p(x,y)
    /// </summary>
    /// <param name="jointProbabilities">Joint probability distribution p(x,y)</param>
    public static double JointEntropy(double[,] jointProbabilities)
    {
        var entropy = 0.0;
        var rows = jointProbabilities.GetLength(0);
        var cols = jointProbabilities.GetLength(1);

        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                var p = jointProbabilities[i, j];
                if (p > 0)
                {
                    entropy -= p * Math.Log(p) / _log2;
                }
            }
        }

        return entropy;
    }

    /// <summary>
    ///     Compute conditional entropy H(X|Y) = H(X,Y) - H(Y)
    /// </summary>
    /// <remarks>
    ///     Measures uncertainty of X given knowledge of Y.
    ///     For music: How predictable is the next chord given the current chord?
    /// </remarks>
    public static double ConditionalEntropy(double[,] jointProbabilities)
    {
        var hXy = JointEntropy(jointProbabilities);

        // Compute marginal probabilities for Y
        var cols = jointProbabilities.GetLength(1);
        var marginalY = new double[cols];

        for (var j = 0; j < cols; j++)
        {
            for (var i = 0; i < jointProbabilities.GetLength(0); i++)
            {
                marginalY[j] += jointProbabilities[i, j];
            }
        }

        var hY = ShannonEntropy(marginalY);

        return hXy - hY;
    }

    /// <summary>
    ///     Compute mutual information I(X;Y) = H(X) + H(Y) - H(X,Y)
    /// </summary>
    /// <remarks>
    ///     Measures how much information X and Y share:
    ///     - I = 0: X and Y are independent
    ///     - I = H(X) = H(Y): X and Y are perfectly dependent
    ///     For music: How much does knowing the current chord tell us about the next chord?
    /// </remarks>
    public static double MutualInformation(double[,] jointProbabilities)
    {
        var rows = jointProbabilities.GetLength(0);
        var cols = jointProbabilities.GetLength(1);

        // Compute marginal probabilities
        var marginalX = new double[rows];
        var marginalY = new double[cols];

        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                marginalX[i] += jointProbabilities[i, j];
                marginalY[j] += jointProbabilities[i, j];
            }
        }

        var hX = ShannonEntropy(marginalX);
        var hY = ShannonEntropy(marginalY);
        var hXy = JointEntropy(jointProbabilities);

        return hX + hY - hXy;
    }

    /// <summary>
    ///     Compute Kullback-Leibler divergence D_KL(P||Q) = S p(x) log2(p(x)/q(x))
    /// </summary>
    /// <param name="p">True distribution</param>
    /// <param name="q">Approximating distribution</param>
    /// <returns>KL divergence in bits (always = 0)</returns>
    /// <remarks>
    ///     Measures how different Q is from P:
    ///     - D_KL = 0: P and Q are identical
    ///     - D_KL > 0: P and Q differ (not symmetric!)
    ///     For music: Compare actual progression frequencies vs. expected/theoretical
    /// </remarks>
    public static double KullbackLeiblerDivergence(IEnumerable<double> p, IEnumerable<double> q)
    {
        var pArray = p.ToArray();
        var qArray = q.ToArray();

        if (pArray.Length != qArray.Length)
        {
            throw new ArgumentException("Distributions must have same length");
        }

        var divergence = 0.0;

        for (var i = 0; i < pArray.Length; i++)
        {
            if (pArray[i] > 0)
            {
                if (qArray[i] <= 0)
                {
                    return double.PositiveInfinity; // Q has zero probability where P doesn't
                }

                divergence += pArray[i] * Math.Log(pArray[i] / qArray[i]) / _log2;
            }
        }

        return divergence;
    }

    /// <summary>
    ///     Compute Jensen-Shannon divergence (symmetric version of KL divergence)
    /// </summary>
    /// <remarks>
    ///     JSD(P||Q) = 0.5 * D_KL(P||M) + 0.5 * D_KL(Q||M) where M = 0.5(P + Q)
    ///     - Symmetric: JSD(P||Q) = JSD(Q||P)
    ///     - Bounded: 0 = JSD = 1 (in bits)
    ///     - Metric: vJSD is a true distance metric
    /// </remarks>
    public static double JensenShannonDivergence(IEnumerable<double> p, IEnumerable<double> q)
    {
        var pArray = p.ToArray();
        var qArray = q.ToArray();

        if (pArray.Length != qArray.Length)
        {
            throw new ArgumentException("Distributions must have same length");
        }

        // Compute mixture M = 0.5(P + Q)
        var m = pArray.Zip(qArray, (pi, qi) => 0.5 * (pi + qi)).ToArray();

        var dKlPm = KullbackLeiblerDivergence(pArray, m);
        var dKlQm = KullbackLeiblerDivergence(qArray, m);

        return 0.5 * dKlPm + 0.5 * dKlQm;
    }

    /// <summary>
    ///     Compute perplexity = 2^H(X)
    /// </summary>
    /// <remarks>
    ///     Perplexity is the effective number of equally likely outcomes:
    ///     - Perplexity = 1: Completely predictable
    ///     - Perplexity = n: Uniform distribution over n outcomes
    ///     For music: "How many chords could plausibly come next?"
    /// </remarks>
    public static double Perplexity(IEnumerable<double> probabilities)
    {
        var entropy = ShannonEntropy(probabilities);
        return Math.Pow(2, entropy);
    }

    /// <summary>
    ///     Compute normalized entropy (0 to 1)
    /// </summary>
    /// <remarks>
    ///     H_norm = H(X) / log2(n) where n is the number of outcomes
    ///     - 0: Completely predictable
    ///     - 1: Maximum uncertainty (uniform distribution)
    /// </remarks>
    public static double NormalizedEntropy(IEnumerable<double> probabilities)
    {
        var probs = probabilities.ToArray();
        if (probs.Length <= 1)
        {
            return 0;
        }

        var entropy = ShannonEntropy(probs);
        var maxEntropy = Math.Log(probs.Length) / _log2;

        return maxEntropy > 0 ? entropy / maxEntropy : 0;
    }

    /// <summary>
    ///     Compute information gain (reduction in entropy)
    /// </summary>
    /// <remarks>
    ///     IG = H(before) - H(after)
    ///     Measures how much uncertainty is reduced by learning something new.
    /// </remarks>
    public static double InformationGain(IEnumerable<double> before, IEnumerable<double> after)
    {
        return ShannonEntropy(before) - ShannonEntropy(after);
    }
}
