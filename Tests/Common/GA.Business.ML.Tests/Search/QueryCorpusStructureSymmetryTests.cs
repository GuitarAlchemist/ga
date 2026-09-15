namespace GA.Business.ML.Tests.Search;

using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Rag;
using GA.Business.ML.Search;
using GA.Business.ML.Tests.TestInfrastructure;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Services.Fretboard.Voicings.Analysis;

/// <summary>
///     A chord query and an indexed voicing of the same chord must land on the same STRUCTURE, MODAL and
///     ROOT vectors, so an exact pitch-class set outranks its subsets
///     (docs/plans/2026-05-12-icv-format-reconciliation-plan.md).
/// </summary>
[TestFixture]
public class QueryCorpusStructureSymmetryTests
{
    // Standard tuning, string 1 (high E) first, as VoicingGenerator orders positions.
    private static readonly int[] OpenStringMidi = [64, 59, 55, 50, 45, 40];

    [TestCase("0-0-0-2-3-x", "Cmaj7", 0, new[] { 0, 4, 7, 11 }, TestName = "Open Cmaj7")]
    [TestCase("1-1-2-0-x-x", "Dm7", 2, new[] { 0, 2, 5, 9 }, TestName = "Dm7 on the top strings")]
    public async Task QueryAndIndexedVoicing_ScoreTheActivePartitionsInFull(
        string stringOneFirstDiagram, string symbol, int root, int[] pitchClasses)
    {
        var voicing = BuildVoicing(stringOneFirstDiagram);
        var doc = VoicingDocumentFactory.FromAnalysis(voicing, VoicingAnalyzer.Analyze(voicing));
        var raw = await TestServices.CreateGenerator().GenerateEmbeddingAsync(doc);
        var corpus = EmbeddingSchema.ExtractCompact(raw.Select(x => (double)x).ToArray());
        var query = new MusicalQueryEncoder(new ModalVectorService())
            .Encode(new StructuredQuery(symbol, root, pitchClasses, null, null));

        Assert.Multiple(() =>
        {
            foreach (var name in new[] { "STRUCTURE", "MODAL", "ROOT" })
            {
                var partition = EmbeddingSchema.SimilarityPartitions.Single(p => p.Name == name);
                Assert.That(PartitionDot(query, corpus, name), Is.EqualTo(partition.SimilarityWeight).Within(1e-3), name);
            }
        });
    }

    private static double PartitionDot(double[] a, double[] b, string name)
    {
        var start = 0;
        foreach (var p in EmbeddingSchema.SimilarityPartitions)
        {
            if (p.Name == name)
            {
                var dot = 0.0;
                for (var j = 0; j < p.Dim; j++) dot += a[start + j] * b[start + j];
                return dot;
            }

            start += p.Dim;
        }

        throw new ArgumentException(name);
    }

    private static Voicing BuildVoicing(string stringOneFirstDiagram)
    {
        var positions = new List<Position>();
        var notes = new List<MidiNote>();
        var parts = stringOneFirstDiagram.Split('-');
        for (var i = 0; i < parts.Length; i++)
        {
            var str = new Str(i + 1);
            if (parts[i] == "x")
            {
                positions.Add(new Position.Muted(str));
                continue;
            }

            var fret = int.Parse(parts[i]);
            var note = new MidiNote(OpenStringMidi[i] + fret);
            positions.Add(new Position.Played(new PositionLocation(str, new Fret(fret)), note));
            notes.Add(note);
        }

        return new Voicing([.. positions], [.. notes]);
    }
}
