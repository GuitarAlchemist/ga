namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Rag.Models;

[TestFixture]
public class ModalVectorServiceTests
{
    // Each mode's own notes, from its root. Its MODAL slot must light up for them. These eight slots
    // were looked up by display names ("Locrian ♮6", "Dorian ♯4", ...) that the mode catalogue does
    // not use, so they were never written. (The Diminished slot has a different problem: see
    // ModalVectorService.)
    [TestCase(EmbeddingSchema.ModalLocrianNatural6, new[] { 0, 1, 3, 5, 6, 9, 10 })]
    [TestCase(EmbeddingSchema.ModalDorianSharp4, new[] { 0, 2, 3, 6, 7, 9, 10 })]
    [TestCase(EmbeddingSchema.ModalLydianSharp2, new[] { 0, 3, 4, 6, 7, 9, 11 })]
    [TestCase(EmbeddingSchema.ModalAlteredDoubleFlat7, new[] { 0, 1, 3, 4, 6, 8, 9 })]
    [TestCase(EmbeddingSchema.ModalDorianFlat2, new[] { 0, 1, 3, 5, 7, 9, 10 })]
    [TestCase(EmbeddingSchema.ModalLydianAugmented, new[] { 0, 2, 4, 6, 8, 9, 11 })]
    [TestCase(EmbeddingSchema.ModalMixolydianFlat6, new[] { 0, 2, 4, 5, 7, 8, 10 })]
    [TestCase(EmbeddingSchema.ModalLocrianNatural2, new[] { 0, 2, 3, 5, 6, 8, 10 })]
    // Controls that already worked.
    [TestCase(EmbeddingSchema.ModalDorian, new[] { 0, 2, 3, 5, 7, 9, 10 })]
    [TestCase(EmbeddingSchema.ModalLydianDominant, new[] { 0, 2, 4, 6, 7, 9, 10 })]
    public void ModeSlot_IsWrittenForTheModesOwnNotes(int slot, int[] pitchClasses)
    {
        var vector = new ModalVectorService().ComputeEmbedding(Doc(pitchClasses));

        Assert.That(vector[slot - EmbeddingSchema.ModalOffset], Is.GreaterThan(0));
    }

    [Test]
    public void AtonalModalEmbedding_HasThePartitionsLength()
    {
        var vector = new ModalVectorService().ComputeAtonalModalEmbedding(Doc([0, 4, 7]));

        Assert.That(vector, Has.Length.EqualTo(EmbeddingSchema.GetPartition("ATONAL_MODAL").Dim));
    }

    private static ChordVoicingRagDocument Doc(int[] pitchClasses) => new()
    {
        PitchClasses = pitchClasses,
        RootPitchClass = 0,
        SearchableText = "",
        AnalysisEngine = nameof(ModalVectorServiceTests),
        AnalysisVersion = "test",
        Jobs = [],
        TuningId = "Standard",
        PitchClassSetId = "",
        MidiNotes = [],
        Diagram = "",
        YamlAnalysis = "",
        IntervalClassVector = "",
        PitchClassSet = "",
        SemanticTags = [],
        PossibleKeys = [],
    };
}
