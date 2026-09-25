namespace GA.Domain.Core.Tests.Theory.Tonal.Scales;

using GA.Domain.Core.Theory.Tonal.Scales;
using NUnit.Framework;

[TestFixture]
public class ScaleMetadataRegistryTests
{
    [Test]
    public void MajorScale_GetMetadata_ReturnsUnifiedMetadata()
    {
        var scale = Scale.Major;
        var metadata = ScaleMetadataRegistry.GetMetadata(scale);

        Assert.That(metadata.BinaryScaleId, Is.EqualTo(2741));
        Assert.That(metadata.Name, Is.EqualTo("Major"));
        Assert.That(metadata.ForteNumber, Is.Not.Null);
        Assert.That(metadata.Modes.Count, Is.GreaterThan(0));
    }
}
