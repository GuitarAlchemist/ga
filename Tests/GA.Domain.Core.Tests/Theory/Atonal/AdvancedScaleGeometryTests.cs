namespace GA.Domain.Core.Tests.Theory.Atonal;

using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal.Scales;
using NUnit.Framework;

[TestFixture]
public class AdvancedScaleGeometryTests
{
    [Test]
    public void MajorScale_GetTertianTriads_Finds7Triads()
    {
        var scale = Scale.Major;
        var triads = scale.PitchClassSet.GetTertianTriads();
        Assert.That(triads.Count, Is.EqualTo(7));
    }

    [Test]
    public void MajorScale_GetParsimoniousTriadConnections_FindsVoiceLeadingMoves()
    {
        var scale = Scale.Major;
        var connections = scale.PitchClassSet.GetParsimoniousTriadConnections();
        Assert.That(connections.Count, Is.GreaterThan(0));
    }

    [Test]
    public void MajorScale_GetOpticK216Embedding_Returns216DimensionalVector()
    {
        var scale = Scale.Major;
        var embedding = scale.PitchClassSet.GetOpticK216Embedding();
        Assert.That(embedding.Length, Is.EqualTo(216));
    }

    [Test]
    public void MajorScale_GetIntervalMatrixDiagnostics_HasZeroContradictions()
    {
        var scale = Scale.Major;
        var (contradictions, ambiguities) = scale.PitchClassSet.GetIntervalMatrixDiagnostics();
        Assert.That(contradictions, Is.EqualTo(0));
    }
}
