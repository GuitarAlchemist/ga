namespace GA.Business.Core.Tests.InformationTheory;

using GA.Core.Numerics;
using NUnit.Framework;

[TestFixture]
public class EntropyMetricsTests
{
    [Test]
    public void ShannonEntropy_UniformDistribution_ReturnsCorrectBits()
    {
        double[] p = [0.5, 0.5];
        var entropy = EntropyMetrics.ShannonEntropy(p);
        Assert.That(entropy, Is.EqualTo(1.0).Within(1e-10));
    }

    [Test]
    public void ShannonEntropy_Unnormalized_NormalizesCorrectly()
    {
        double[] p = [1.0, 1.0];
        var entropy = EntropyMetrics.ShannonEntropy(p);
        Assert.That(entropy, Is.EqualTo(1.0).Within(1e-10));
    }

    [Test]
    public void InformationGain_KLDivergence_ReturnsCorrectValue()
    {
        double[] before = [0.5, 0.5]; // Uniform prior
        double[] after = [1.0, 0.0]; // Certainty posterior
        var gain = EntropyMetrics.InformationGain(before, after);
        // D_KL(after || before) = 1.0 * log2(1.0/0.5) + 0.0 = 1.0
        Assert.That(gain, Is.EqualTo(1.0).Within(1e-10));
    }

    [Test]
    public void InformationGain_IdenticalDistributions_ReturnsZero()
    {
        double[] before = [0.8, 0.2];
        double[] after = [0.8, 0.2];
        var gain = EntropyMetrics.InformationGain(before, after);
        Assert.That(gain, Is.EqualTo(0.0).Within(1e-10));
    }

    [Test]
    public void InformationGain_PermutedDistributions_ReturnsPositive()
    {
        double[] before = [0.8, 0.2];
        double[] after = [0.2, 0.8];
        var gain = EntropyMetrics.InformationGain(before, after);
        // D_KL([0.2, 0.8] || [0.8, 0.2]) = 0.2 * log2(0.2/0.8) + 0.8 * log2(0.8/0.2)
        // = 0.2 * (-2) + 0.8 * 2 = -0.4 + 1.6 = 1.2
        Assert.That(gain, Is.EqualTo(1.2).Within(1e-10));
    }

    [Test]
    public void JointAndMutualInformation_Unnormalized_ReturnsCorrectValues()
    {
        double[,] joint = { { 1.0, 0.0 }, { 0.0, 1.0 } }; // Unnormalized joint: will be normalized to 0.5, 0.5
        var hXy = EntropyMetrics.JointEntropy(joint);
        var mi = EntropyMetrics.MutualInformation(joint);

        Assert.That(hXy, Is.EqualTo(1.0).Within(1e-10));
        Assert.That(mi, Is.EqualTo(1.0).Within(1e-10)); // Perfect dependence
    }

    [Test]
    public void KullbackLeiblerDivergence_UniformToCertainty()
    {
        double[] p = [1.0, 0.0];
        double[] q = [0.5, 0.5];
        var divergence = EntropyMetrics.KullbackLeiblerDivergence(p, q);
        Assert.That(divergence, Is.EqualTo(1.0).Within(1e-10));
    }
}
