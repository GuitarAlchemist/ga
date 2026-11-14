namespace GA.Domain.Core.Tests.Theory.Tonal;

using GA.Domain.Core.Theory.Tonal.Modes;
using GA.Domain.Core.Theory.Tonal.Modes.Symmetric;

/// <summary>
///     The two diminished (octatonic) scales are named by their first step:
///     <see href="https://en.wikipedia.org/wiki/Octatonic_scale" />.
/// </summary>
[TestFixture]
public class DiminishedScaleModeTests
{
    private static int FirstStep(ScaleMode mode)
    {
        var pitchClasses = mode.Notes.Select(note => note.PitchClass.Value).ToList();
        return (pitchClasses[1] - pitchClasses[0] + 12) % 12;
    }

    [Test]
    public void HalfWhole_StartsWithAHalfStep() =>
        Assert.Multiple(() =>
        {
            Assert.That(DiminishedScaleMode.HalfWhole.Name, Is.EqualTo("Half-whole diminished"));
            Assert.That(FirstStep(DiminishedScaleMode.HalfWhole), Is.EqualTo(1));
        });

    [Test]
    public void WholeHalf_StartsWithAWholeStep() =>
        Assert.Multiple(() =>
        {
            Assert.That(DiminishedScaleMode.WholeHalf.Name, Is.EqualTo("Whole-half diminished"));
            Assert.That(FirstStep(DiminishedScaleMode.WholeHalf), Is.EqualTo(2));
        });
}
