namespace GaMcpServer.Tests;

using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using GA.Business.ML.Embeddings;
using GaMcpServer.Tools;

/// <summary>
///     <c>ga_generate_voicing_embedding</c> and <c>ga_get_embedding_schema</c> must describe the schema
///     the generator actually uses (<c>EmbeddingSchema</c>), not a hand-copied older layout, and the
///     diagram in the parameter description must be the chord that description names. The layout
///     behaviour of the same tool is covered by <see cref="VoicingEmbeddingToolTests" />.
/// </summary>
[TestFixture]
public sealed class VoicingEmbeddingToolSchemaTests
{
    [Test]
    public void GetEmbeddingSchema_ListsEveryRegistryPartition_IncludingRoot()
    {
        using var json = JsonDocument.Parse(VoicingEmbeddingTool.GaGetEmbeddingSchema());
        var root = json.RootElement;
        var partitions = root.GetProperty("partitions").EnumerateArray().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("totalDimension").GetInt32(), Is.EqualTo(EmbeddingSchema.TotalDimension));
            Assert.That(partitions.Select(p => p.GetProperty("name").GetString()),
                Is.EqualTo(EmbeddingSchema.Partitions.Select(p => p.Name)));
            Assert.That(partitions.Sum(p => p.GetProperty("dim").GetInt32()), Is.EqualTo(EmbeddingSchema.TotalDimension),
                "partition dims must tile the whole vector");

            var rootPartition = partitions.Single(p => p.GetProperty("name").GetString() == "ROOT");
            Assert.That(rootPartition.GetProperty("offset").GetInt32(), Is.EqualTo(228));
            Assert.That(rootPartition.GetProperty("dim").GetInt32(), Is.EqualTo(12));

            var hierarchy = partitions.Single(p => p.GetProperty("name").GetString() == "HIERARCHY");
            Assert.That(hierarchy.GetProperty("dim").GetInt32(), Is.EqualTo(15));
            var atonal = partitions.Single(p => p.GetProperty("name").GetString() == "ATONAL_MODAL");
            Assert.That(atonal.GetProperty("dim").GetInt32(), Is.EqualTo(64));
        });
    }

    [Test]
    public void GenerateVoicingEmbedding_DescriptionHasNoStaleDimension_AndItsExampleIsTheChordItNames()
    {
        var method = typeof(VoicingEmbeddingTool).GetMethod(nameof(VoicingEmbeddingTool.GaGenerateVoicingEmbedding))!;
        var toolDescription = method.GetCustomAttribute<DescriptionAttribute>()!.Description;
        var diagramDescription = method.GetParameters()[0].GetCustomAttribute<DescriptionAttribute>()!.Description;

        Assert.Multiple(() =>
        {
            Assert.That(toolDescription, Does.Not.Contain("228"));
            Assert.That(diagramDescription, Does.Contain("'0-1-0-2-3-x' for open C major"));
        });
    }

    [Test]
    public async Task GenerateVoicingEmbedding_DescribedExample_IsCMajor()
    {
        using var json = JsonDocument.Parse(await VoicingEmbeddingTool.GaGenerateVoicingEmbedding("0-1-0-2-3-x", "guitar"));
        var metadata = json.RootElement.GetProperty("metadata");

        Assert.Multiple(() =>
        {
            Assert.That(metadata.GetProperty("chordName").GetString(), Does.StartWith("C"));
            Assert.That(metadata.GetProperty("midiNotes").EnumerateArray().Select(n => n.GetInt32() % 12).Distinct().Order(),
                Is.EqualTo(new[] { 0, 4, 7 }));
            Assert.That(json.RootElement.GetProperty("dimension").GetInt32(), Is.EqualTo(EmbeddingSchema.TotalDimension));
        });
    }
}
