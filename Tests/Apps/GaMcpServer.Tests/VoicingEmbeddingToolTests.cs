namespace GaMcpServer.Tests;

using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using FretboardVoicingsCLI;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Search;
using GaMcpServer.Tools;

/// <summary>
///     <c>ga_generate_voicing_embedding</c> layouts. The compact layout must be byte-for-byte the
///     row the OPTIC-K index writer stores, so GA output can be fed straight to IX's
///     <c>ix_optick_search</c> (which rejects queries whose dimension differs from the index).
/// </summary>
[TestFixture]
public sealed class VoicingEmbeddingToolTests
{
    private const string Diagram = "x-3-2-0-1-0";
    private const string Instrument = "guitar";

    private static async Task<(int Dimension, string Layout, float[] Embedding, int[] MidiNotes)> Generate(
        string diagram, string instrument, string? layout = null)
    {
        var json = layout is null
            ? await VoicingEmbeddingTool.GaGenerateVoicingEmbedding(diagram, instrument)
            : await VoicingEmbeddingTool.GaGenerateVoicingEmbedding(diagram, instrument, layout);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (
            root.GetProperty("dimension").GetInt32(),
            root.GetProperty("layout").GetString()!,
            root.GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray(),
            root.GetProperty("metadata").GetProperty("midiNotes").EnumerateArray().Select(e => e.GetInt32()).ToArray());
    }

    private static double MaxAbsDiff(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var max = 0.0;
        for (var i = 0; i < a.Length; i++) max = Math.Max(max, Math.Abs(a[i] - b[i]));
        return max;
    }

    [Test]
    public async Task Default_IsRawLayout_WithTotalDimension()
    {
        var (dimension, layout, embedding, _) = await Generate(Diagram, Instrument);

        Assert.Multiple(() =>
        {
            Assert.That(layout, Is.EqualTo("raw"));
            Assert.That(dimension, Is.EqualTo(EmbeddingSchema.TotalDimension));
            Assert.That(embedding, Has.Length.EqualTo(EmbeddingSchema.TotalDimension));
        });
    }

    [Test]
    public async Task Compact_DeclaresCompactDimension_AndIndexLayout()
    {
        var (dimension, layout, embedding, _) = await Generate(Diagram, Instrument, "compact");

        Assert.Multiple(() =>
        {
            Assert.That(dimension, Is.EqualTo(EmbeddingSchema.CompactDimension));
            Assert.That(dimension, Is.EqualTo(OptickIndexReader.Dimension));
            Assert.That(embedding, Has.Length.EqualTo(dimension));
            Assert.That(layout, Is.EqualTo(EmbeddingSchema.CompactLayoutV4));
        });
    }

    [Test]
    public async Task Compact_EqualsRowWrittenByOptickIndexWriter()
    {
        var (_, _, raw, midiNotes) = await Generate(Diagram, Instrument, "raw");
        var (_, _, compact, _) = await Generate(Diagram, Instrument, "compact");

        var path = Path.Combine(Path.GetTempPath(), $"ga-compact-embedding-{Guid.NewGuid():N}.index");
        try
        {
            using (var writer = new OptickIndexWriter(path))
            {
                writer.WriteIndex([new VoicingEntry(raw, Diagram, Instrument, midiNotes, null)]);
            }

            using var reader = new OptickIndexReader(path);
            var row = reader.GetVector(0);
            var diff = MaxAbsDiff(compact, row);
            TestContext.Out.WriteLine($"max |compact - index row| = {diff:E3}");
            Assert.That(diff, Is.LessThan(1e-6));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void UnknownLayout_Throws()
    {
        Assert.ThrowsAsync<ArgumentException>(() =>
            VoicingEmbeddingTool.GaGenerateVoicingEmbedding(Diagram, Instrument, "packed"));
    }

    [Test]
    public void Description_StatesSchemaDimensions()
    {
        var description = typeof(VoicingEmbeddingTool)
            .GetMethod(nameof(VoicingEmbeddingTool.GaGenerateVoicingEmbedding))!
            .GetCustomAttribute<DescriptionAttribute>()!.Description;

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain($"({EmbeddingSchema.TotalDimension})"));
            Assert.That(description, Does.Contain($"({EmbeddingSchema.CompactDimension})"));
            Assert.That(description, Does.Not.Contain("228"));
        });
    }

    /// <summary>
    ///     Local-only: the compact output for voicings sampled from the real
    ///     <c>state/voicings/optick.index</c> matches their stored rows. Skipped when the index is absent.
    ///     Every partition except STRUCTURE must match exactly. STRUCTURE only warns: an index built
    ///     before the ICV parse fix (ga#199) holds misparsed ICV dims, so it drifts from the current
    ///     generator until the index is rebuilt (optic-k-rebuild skill).
    /// </summary>
    [Test]
    [Category("Integration")]
    public async Task Compact_MatchesRealIndexRows_WhenIndexPresent()
    {
        var indexPath = FindIndexPath();
        if (indexPath is null) Assert.Ignore("state/voicings/optick.index not present");

        var structure = EmbeddingSchema.GetPartition("STRUCTURE");
        Assert.That(EmbeddingSchema.SimilarityPartitions.First(), Is.EqualTo(structure),
            "STRUCTURE is expected to be the first compact partition");

        using var reader = new OptickIndexReader(indexPath!);
        var (first, count) = reader.GetInstrumentRange(Instrument);
        double worstOther = 0.0, worstStructure = 0.0;
        for (var k = 0; k < 20; k++)
        {
            var i = first + k * (count / 20);
            var meta = reader.GetMetadata(i);
            var (_, _, compact, midiNotes) = await Generate(meta.Diagram, meta.Instrument, "compact");
            var row = reader.GetVector(i);

            Assert.That(midiNotes, Is.EquivalentTo(meta.MidiNotes), $"row {i} '{meta.Diagram}' notes");
            var structureDiff = MaxAbsDiff(compact.AsSpan(0, structure.Dim), row[..structure.Dim]);
            var otherDiff = MaxAbsDiff(compact.AsSpan(structure.Dim), row[structure.Dim..]);
            TestContext.Out.WriteLine(
                $"row {i} '{meta.Diagram}': STRUCTURE max abs diff {structureDiff:E3}, other partitions {otherDiff:E3}");
            worstStructure = Math.Max(worstStructure, structureDiff);
            worstOther = Math.Max(worstOther, otherDiff);
        }

        Assert.That(worstOther, Is.LessThan(1e-5), "non-STRUCTURE partitions must equal the index rows");
        if (worstStructure >= 1e-5)
        {
            Assert.Warn($"STRUCTURE differs from the index (max abs diff {worstStructure:E3}): " +
                        "the index predates the current generator; rebuild it.");
        }
    }

    private static string? FindIndexPath()
    {
        var envOverride = Environment.GetEnvironmentVariable("GA_OPTICK_INDEX_PATH");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride)) return envOverride;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "state", "voicings", "optick.index");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
