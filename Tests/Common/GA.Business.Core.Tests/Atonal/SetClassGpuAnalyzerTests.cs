namespace GA.Business.Core.Tests.Atonal;

using System.Linq;
using Core.Analysis.Gpu;
using Core.Atonal;
using Core.Notes;
using Extensions;
using ILGPU.Runtime;

[TestFixture]
public class SetClassGpuAnalyzerTests
{
    [Test]
    public void AnalyzeSpectra_MatchesCpuMagnitudes()
    {
        // Arrange
        var cMajor = new SetClass(AccidentedNoteCollection.Parse("C E G").ToPitchClassSet());
        var dMajor = new SetClass(AccidentedNoteCollection.Parse("D F# A").ToPitchClassSet());
        var sets = new[] { cMajor, dMajor };

        using var analyzer = new SetClassGpuAnalyzer();

        // Act
        var gpuResults = analyzer.AnalyzeSpectra(sets);

        // Assert
        Assert.That(gpuResults, Has.Count.EqualTo(sets.Length));

        for (var i = 0; i < sets.Length; i++)
        {
            var cpuMagnitudes = sets[i].GetMagnitudeSpectrum();
            var gpuMagnitudes = gpuResults[i].MagnitudeSpectrum;

            Assert.That(gpuMagnitudes.Length, Is.EqualTo(cpuMagnitudes.Length));

            for (var k = 0; k < cpuMagnitudes.Length; k++)
            {
                Assert.That(gpuMagnitudes[k], Is.EqualTo(cpuMagnitudes[k]).Within(1e-6),
                    $"Magnitude mismatch at bin {k}");
            }
        }
    }

    [Test]
    public void AnalyzeSpectra_ComputesCentroidCloseToCpu()
    {
        // Arrange
        var singleton = new SetClass(AccidentedNoteCollection.Parse("C").ToPitchClassSet());
        using var analyzer = new SetClassGpuAnalyzer();

        // Act
        var result = analyzer.AnalyzeSpectra(new[] { singleton }).Single();

        // Assert
        Assert.That(result.SpectralCentroid, Is.EqualTo(singleton.GetSpectralCentroid()).Within(1e-6));
    }

    [Test]
    public void Provider_CachesCpuAnalyzerInstances()
    {
        // Act
        var analyzer1 = SetClassGpuAnalyzerProvider.GetAnalyzer(preferGpu: false);
        var analyzer2 = SetClassGpuAnalyzerProvider.GetAnalyzer(preferGpu: false);

        // Assert
        Assert.That(ReferenceEquals(analyzer1, analyzer2), Is.True);
        Assert.That(analyzer1.AcceleratorType, Is.EqualTo(AcceleratorType.CPU));
    }
}
