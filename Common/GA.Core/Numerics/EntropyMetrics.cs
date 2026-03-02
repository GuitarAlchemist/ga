namespace GA.Core.Numerics;

/// <summary>
/// Provides information-theoretic metrics such as Shannon Entropy and KL Divergence.
/// </summary>
public static class EntropyMetrics
{
    /// <summary>
    /// Computes Shannon entropy in bits for a given probability distribution.
    /// Normalizes input automatically if not already normalized.
    /// </summary>
    public static double ShannonEntropy(IEnumerable<double> probabilities)
    {
        var p = probabilities.ToList();
        var sum = p.Sum();
        if (sum <= 1e-15) return 0;

        var entropy = 0.0;
        foreach (var val in p)
        {
            if (!(val > 1e-15)) continue; // Skip zero values

            // Normalize and compute entropy
            var normalized = val / sum;
            entropy -= normalized * Math.Log2(normalized);
        }
        return entropy;
    }

    /// <summary>
    /// Computes information gain (relative entropy/KL divergence) from prior to posterior.
    /// </summary>
    public static double InformationGain(IEnumerable<double> before, IEnumerable<double> after) =>
        KullbackLeiblerDivergence(after, before);

    /// <summary>
    /// Computes Kullback-Leibler divergence D_KL(P || Q) in bits.
    /// </summary>
    public static double KullbackLeiblerDivergence(IEnumerable<double> p, IEnumerable<double> q)
    {
        var pList = p.ToList();
        var qList = q.ToList();
        if (pList.Count != qList.Count) throw new ArgumentException("Distributions must have same size");

        var sumP = pList.Sum();
        var sumQ = qList.Sum();
        if (sumP <= 1e-15 || sumQ <= 1e-15) return 0;

        var dkl = 0.0;
        for (var i = 0; i < pList.Count; i++)
        {
            if (!(pList[i] > 1e-15)) continue; // Skip zero values

            // Normalize and compute divergence
            var pn = pList[i] / sumP;
            var qn = qList[i] / sumQ;
            if (qn <= 1e-15) return double.PositiveInfinity;
            dkl += pn * Math.Log2(pn / qn);
        }
        return dkl;
    }

    /// <summary>
    /// Computes joint entropy H(X,Y) for a joint probability distribution table.
    /// </summary>
    public static double JointEntropy(double[,] joint)
    {
        var sum = 0.0;
        foreach (var val in joint) sum += val;
        if (sum <= 1e-15) return 0;

        var h = 0.0;
        foreach (var val in joint)
        {
            if (!(val > 1e-15)) continue; // Skip zero values

            var p = val / sum;
            h -= p * Math.Log2(p);
        }
        return h;
    }

    /// <summary>
    /// Computes Mutual Information I(X;Y) from a joint probability distribution table.
    /// </summary>
    public static double MutualInformation(double[,] joint)
    {
        var rows = joint.GetLength(0);
        var cols = joint.GetLength(1);
        var sum = 0.0;
        foreach (var val in joint) sum += val;
        if (sum <= 1e-15) return 0;

        var rowSums = new double[rows];
        var colSums = new double[cols];
        for (var i = 0; i < rows; i++)
        for (var j = 0; j < cols; j++)
        {
            rowSums[i] += joint[i, j];
            colSums[j] += joint[i, j];
        }

        var mi = 0.0;
        for (var i = 0; i < rows; i++)
        for (var j = 0; j < cols; j++)
        {
            if (joint[i, j] > 1e-15)
            {
                var pXY = joint[i, j] / sum;
                var pX = rowSums[i] / sum;
                var pY = colSums[j] / sum;
                if (pX > 1e-15 && pY > 1e-15)
                {
                    mi += pXY * Math.Log2(pXY / (pX * pY));
                }
            }
        }
        return mi;
    }
}
