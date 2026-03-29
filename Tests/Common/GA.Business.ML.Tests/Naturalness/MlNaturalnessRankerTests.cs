namespace GA.Business.ML.Tests.Naturalness;

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Business.ML.Naturalness;
using Domain.Core.Primitives.Notes;
using Domain.Core.Instruments.Primitives;
using Domain.Services.Fretboard.Analysis;

[TestFixture]
[Category("RequiresModel")]
public class MlNaturalnessRankerTests
{
    private MlNaturalnessRanker _ranker;

    [SetUp]
    public void Setup()
    {
        // Try to locate the model at various relative positions
        var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "naturalness_ranker.onnx");
        TestContext.WriteLine($"Loading model from: {modelPath}");
        Assert.That(File.Exists(modelPath), Is.True, "ONNX model file is missing!");
        _ranker = new MlNaturalnessRanker();
    }

    [TearDown]
    public void Teardown() => _ranker.Dispose();

    [Test]
    public void PredictNaturalness_ReturnsNonNeutralValue()
    {
        // Arrange
        var from = new List<FretboardPosition>
        {
            new(Str.FromValue(1), 5, Pitch.FromMidiNote(60)),
            new(Str.FromValue(2), 7, Pitch.FromMidiNote(64)),
            new(Str.FromValue(3), 7, Pitch.FromMidiNote(67))
        };
        var to = new List<FretboardPosition>
        {
            new(Str.FromValue(1), 5, Pitch.FromMidiNote(60)),
            new(Str.FromValue(2), 7, Pitch.FromMidiNote(64)),
            new(Str.FromValue(3), 6, Pitch.FromMidiNote(66))
        };

        // Act
        var result = _ranker.PredictNaturalness(from, to);

        // Assert
        TestContext.WriteLine($"Predicted naturalness: {result}");
        Assert.That(result, Is.Not.EqualTo(0.5f), "Model returned fallback neutral score, meaning it failed to run.");
    }
}
