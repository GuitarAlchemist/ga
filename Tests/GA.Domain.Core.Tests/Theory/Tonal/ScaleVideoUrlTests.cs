namespace GA.Domain.Core.Tests.Theory.Tonal;

using System;
using System.Linq;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal.Scales;
using NUnit.Framework;

/// <summary>
///     The video URLs of <c>Theory/Tonal/Scales/Data/scale_video_urls.json</c>, embedded in GA.Domain.Core.
/// </summary>
[TestFixture]
public class ScaleVideoUrlTests
{
    private static readonly Uri MajorScaleVideo = new("https://www.youtube.com/embed/xnZZOgAH2Co");

    [Test]
    public void ScaleVideoUrl_IsFoundForEveryScaleOfTheFile()
    {
        var withUrl = Enumerable.Range(0, 4096)
            .Count(id => PitchClassSetId.FromValue(id).ToPitchClassSet().ScaleVideoUrl is not null);

        Assert.That(withUrl, Is.EqualTo(1490));
    }

    [Test]
    public void MajorScale_HasItsVideoUrl() => Assert.Multiple(() =>
    {
        Assert.That(PitchClassSetId.FromValue(2741).ToPitchClassSet().ScaleVideoUrl, Is.EqualTo(MajorScaleVideo));
        Assert.That(ScaleMetadataRegistry.GetMetadata(2741).VideoUrl, Is.EqualTo(MajorScaleVideo));
    });
}
