namespace GA.Business.ML.Tests.Unit;

using System;
using System.Linq;
using System.Threading.Tasks;
using Embeddings;
using Embeddings.Services;
using NUnit.Framework;
using Rag.Models;
using TestInfrastructure;

[TestFixture]
public class ContextVectorServiceTests
{
    private MusicalEmbeddingGenerator _generator = null!;

    [SetUp]
    public void Setup()
    {
        _generator = TestServices.CreateGenerator();
    }

    [Test]
    public void ComputeEmbedding_WithNullMidiNotes_ReturnsZeroForKeyRelationshipSlots()
    {
        // Act
        var v = ContextVectorService.ComputeEmbedding(midiNotes: null);

        // Assert - slots 6-11 are indices 6 to 11
        for (int i = 6; i < 12; i++)
        {
            Assert.That(v[i], Is.EqualTo(0.0));
        }
    }

    [Test]
    public void ComputeEmbedding_WithMidiNotes_PopulatesKeyRelationshipSlots()
    {
        // Arrange - C Major triad voicing
        int[] midiNotes = [48, 52, 55, 60, 64];

        // Act
        var v = ContextVectorService.ComputeEmbedding(midiNotes: midiNotes);

        // Assert
        double sumSq = 0;
        for (int i = 6; i < 12; i++)
        {
            sumSq += v[i] * v[i];
        }

        // Key relationship slots should be populated
        Assert.That(sumSq, Is.GreaterThan(0.0));

        // The L2 norm squared of slots 6-11 scaled by 1/magnitude^2 should equal exactly 3.0 (mathematical property)
        var spec = PhaseSphereService.ComputeWeightedSpectralVector(midiNotes);
        var normalizedSpec = PhaseSphereService.NormalizeToSphere(spec);
        var k5 = normalizedSpec[4];

        Assert.That(k5.Magnitude, Is.GreaterThan(0.0));
        Assert.That(sumSq / (k5.Magnitude * k5.Magnitude), Is.EqualTo(3.0).Within(1e-5));
    }

    [Test]
    public async Task MusicalEmbeddingGenerator_PopulatesContextProxiesAndDerivesHarmonicFunction()
    {
        // Arrange - Tonic/Major chord
        var cMajorDoc = new ChordVoicingRagDocument
        {
            Id = "c-major-open",
            ChordName = "C Major",
            Diagram = "x-3-2-0-1-0",
            MidiNotes = [48, 52, 55, 60, 64],
            PitchClasses = [0, 4, 7],
            PitchClassSet = "{0, 4, 7}",
            IntervalClassVector = "001110",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            SearchableText = "C Major open position",
            Jobs = [], TuningId = "Standard", PitchClassSetId = "3-11", YamlAnalysis = "",
            PossibleKeys = ["C Major"], SemanticTags = ["Major", "Triad", "Open"], StackingType = "Tertian",
            Consonance = 0.8,
            Embedding = null
        };

        // Arrange - Dominant/Diminished chord
        var g7DominantDoc = new ChordVoicingRagDocument
        {
            Id = "g7-dominant",
            ChordName = "G Dominant 7",
            Diagram = "3-2-0-0-0-1",
            MidiNotes = [43, 47, 50, 55, 59, 65],
            PitchClasses = [7, 11, 2, 5],
            PitchClassSet = "{2, 5, 7, 11}",
            IntervalClassVector = "012111",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            SearchableText = "G Dominant 7th chord",
            Jobs = [], TuningId = "Standard", PitchClassSetId = "4-27", YamlAnalysis = "",
            PossibleKeys = ["C Major"], SemanticTags = ["Dominant", "Seventh"], StackingType = "Tertian",
            Consonance = 0.3,
            Embedding = null
        };

        // Act
        var cMajorEmb = await _generator.GenerateEmbeddingAsync(cMajorDoc);
        var g7DominantEmb = await _generator.GenerateEmbeddingAsync(g7DominantDoc);

        // CONTEXT partition is dims 54-65
        var cMajorCtx = cMajorEmb.Skip(54).Take(12).ToArray();
        var g7DominantCtx = g7DominantEmb.Skip(54).Take(12).ToArray();

        // Assert - Tonic proxy derivation
        Assert.That(cMajorCtx[0], Is.EqualTo(1.0f), "C Major should map to Tonic");
        Assert.That(cMajorCtx[1], Is.EqualTo(0.0f));
        Assert.That(cMajorCtx[2], Is.EqualTo(0.0f));

        // Assert - Dominant proxy derivation
        Assert.That(g7DominantCtx[0], Is.EqualTo(0.0f));
        Assert.That(g7DominantCtx[1], Is.EqualTo(0.0f));
        Assert.That(g7DominantCtx[2], Is.EqualTo(1.0f), "G7 Dominant should map to Dominant");

        // Assert - stabilityDelta proxy
        Assert.That(cMajorCtx[3], Is.EqualTo((float)(cMajorDoc.Consonance - 0.5)), "stabilityDelta should match proxy");
        Assert.That(g7DominantCtx[3], Is.EqualTo((float)(g7DominantDoc.Consonance - 0.5)));

        // Assert - tension
        Assert.That(cMajorCtx[4], Is.EqualTo((float)(1.0 - cMajorDoc.Consonance)));
        Assert.That(g7DominantCtx[4], Is.EqualTo((float)(1.0 - g7DominantDoc.Consonance)));

        // Assert - isResolution proxy
        Assert.That(cMajorCtx[5], Is.EqualTo(1.0f), "C Major isResolution should be 1.0 (Consonance > 0.7)");
        Assert.That(g7DominantCtx[5], Is.EqualTo(0.0f), "G7 isResolution should be 0.0 (Consonance <= 0.7)");
    }
}
