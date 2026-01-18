namespace GA.Business.ML.Tests.Wavelets;

using System;
using System.Linq;
using GA.Business.ML.Wavelets;
using NUnit.Framework;

[TestFixture]
public class WaveletTests
{
    private WaveletTransformService _service;

    [SetUp]
    public void Setup()
    {
        _service = new();
    }

    [Test]
    public void Test_ComputeAdaptiveLevels()
    {
        Assert.That(WaveletTransformService.ComputeAdaptiveLevels(8), Is.EqualTo(1));
        Assert.That(WaveletTransformService.ComputeAdaptiveLevels(16), Is.EqualTo(2));
        Assert.That(WaveletTransformService.ComputeAdaptiveLevels(32), Is.EqualTo(3));
        Assert.That(WaveletTransformService.ComputeAdaptiveLevels(64), Is.EqualTo(3)); // Capped at 3
    }

    [Test]
    public void Test_Haar_ConstantSignal()
    {
        // Haar wavelet on constant signal should produce 0 detail
        // Input: [1, 1, 1, 1]
        // Step 1:
        // Apx = [(1*c + 1*c), (1*c + 1*c)] = [2c, 2c] where c = 1/sqrt(2) ≈ 0.707
        // Det = [(1*c - 1*c), (1*c - 1*c)] = [0, 0]

        var signal = new double[] { 1, 1, 1, 1 };
        var decomp = _service.Decompose(signal, WaveletTransformService.WaveletFamily.Haar, levels: 1);

        var details = decomp.DetailCoefficients[0];
        Assert.That(details.All(d => Math.Abs(d) < 1e-10), Is.True, "Haar details should be zero for constant signal");

        var approx = decomp.ApproximationCoefficients;
        Assert.That(approx.All(a => a > 1.0), Is.True, "Approximation should capture energy");
    }

    [Test]
    public void Test_Daubechies4_StepSignal()
    {
        // Step function: [0, 0, 0, 0, 1, 1, 1, 1]
        // Should produce a detail spike at the transition
        var signal = new double[] { 0, 0, 0, 0, 1, 1, 1, 1 };
        var decomp = _service.Decompose(signal, WaveletTransformService.WaveletFamily.Daubechies4, levels: 1);

        var details = decomp.DetailCoefficients[0];
        // The transition is in the middle, so we expect energy there
        var energy = details.Sum(x => x * x);
        Assert.That(energy, Is.GreaterThan(0), "Db4 should detect step edge");
    }

    [Test]
    public void Test_FeatureExtraction_Dimensions()
    {
        var signal = new double[32]; // Zeros
        var decomp = _service.Decompose(signal); // levels=3
        var features = _service.ExtractFeatures(decomp);

        // Features: 4 stats * (1 Apx + 3 Det) = 16 dimensions
        Assert.That(features.Length, Is.EqualTo(16));
    }
}
