namespace GA.Business.ML.Tests;

using System.Linq;
using GA.Business.ML.Embeddings;
using NUnit.Framework;

[TestFixture]
public class SpectralRetrievalTests
{
    [Test]
    public void Test_WeightedSimilarity_PrioritizesStructure()
    {
        // Arrange
        // Vector A: Baseline
        var vecA = new double[EmbeddingSchema.TotalDimension];
        // Vector B: Identical Structure (6-29), Different Symbolic (66-77)
        var vecB = new double[EmbeddingSchema.TotalDimension];
        // Vector C: Different Structure, Identical Symbolic
        var vecC = new double[EmbeddingSchema.TotalDimension];

        // Fill Structure (High Weight: 0.45)
        for (int i = 6; i < 30; i++) { vecA[i] = 1; vecB[i] = 1; vecC[i] = 0; }
        // Fill Symbolic (Low Weight: 0.10)
        for (int i = 66; i < 78; i++) { vecA[i] = 1; vecB[i] = 0; vecC[i] = 1; }

        // Act
        var scoreAB = SpectralRetrievalService.CalculateWeightedSimilarity(vecA, vecB, SpectralRetrievalService.SearchPreset.Tonal);
        var scoreAC = SpectralRetrievalService.CalculateWeightedSimilarity(vecA, vecC, SpectralRetrievalService.SearchPreset.Tonal);

        // Assert
        TestContext.WriteLine($"Score A vs B (Matching Structure): {scoreAB:F4}");
        TestContext.WriteLine($"Score A vs C (Matching Symbolic): {scoreAC:F4}");
        TestContext.WriteLine($"Comparison: Expected ScoreAB > ScoreAC, Actual Result={scoreAB > scoreAC} (OPTIC-K schema prioritizes structure (45%) over symbolic metadata (10%))");

        Assert.That(scoreAB, Is.GreaterThan(scoreAC), "Structure match (45% weight) should outweigh Symbolic match (10% weight) according to retrieval rules.");
    }

    [Test]
    public void Test_CosineSimilarity_SIMD()
    {
        // Arrange
        // Simple test to ensure the SIMD implementation is mathematically correct
        var a = new double[216];
        var b = new double[216];

        // Orthogonal vectors in Structure partition
        a[6] = 1;
        b[7] = 1;

        // Act
        var scoreOrthogonal = SpectralRetrievalService.CalculateWeightedSimilarity(a, b, SpectralRetrievalService.SearchPreset.Tonal);
        var scoreSelf = SpectralRetrievalService.CalculateWeightedSimilarity(a, a, SpectralRetrievalService.SearchPreset.Tonal);

        // Assert
        TestContext.WriteLine($"Score Orthogonal: {scoreOrthogonal:F4}");
        TestContext.WriteLine($"Score Self (only Structure partition populated): {scoreSelf:F4}");

        Assert.Multiple(() =>
        {
            Assert.That(scoreOrthogonal, Is.EqualTo(0).Within(1e-6));
            // Structure Weight = 0.45. Cosine(a, a) = 1. Result should be 0.45.
            Assert.That(scoreSelf, Is.EqualTo(0.45).Within(1e-6));
        });
    }
}
