namespace GA.Business.ML.Tests.Search;

using GA.Business.ML.Embeddings;
using GA.Business.ML.Search;

/// <summary>
///     <see cref="OptickIndexReader.Dimension"/> is read once instead of summing the partition
///     registry on every <see cref="OptickIndexReader.GetVector"/> call. These tests pin the value
///     it caches to the registry it used to recompute.
/// </summary>
[TestFixture]
public class OptickIndexReaderDimensionTests
{
    [Test]
    public void Dimension_EqualsSumOfSimilarityPartitionDims()
    {
        var expected = EmbeddingSchema.SimilarityPartitions.Sum(p => p.Dim);

        Assert.That(OptickIndexReader.Dimension, Is.EqualTo(expected));
        Assert.That(OptickIndexReader.Dimension, Is.EqualTo(EmbeddingSchema.CompactDimension));
    }

    [Test]
    public void Dimension_IsStableAcrossReadsAndThreads()
    {
        var first = OptickIndexReader.Dimension;
        var seen = new System.Collections.Concurrent.ConcurrentBag<int>();

        Parallel.For(0, 10_000, _ => seen.Add(OptickIndexReader.Dimension));

        Assert.That(seen.Distinct(), Is.EquivalentTo(new[] { first }));
    }
}
