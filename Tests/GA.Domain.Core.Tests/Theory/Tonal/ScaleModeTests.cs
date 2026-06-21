namespace GA.Domain.Core.Tests.Theory.Tonal;

using System.Linq;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal.Modes.Diatonic;
using GA.Domain.Core.Theory.Tonal.Primitives.Diatonic;
using GA.Domain.Core.Theory.Tonal.Scales;
using NUnit.Framework;

/// <summary>
///     Characterization tests for the modes of the major scale (<see cref="MajorScaleMode" />).
///     The seven modes are rotations of the parent <see cref="Scale.Major" />; each has a distinct
///     tonal centre and name but shares the parent's pitch-class content.
/// </summary>
[TestFixture]
public class ScaleModeTests
{
    [Test]
    public void Items_ReturnsSevenModes()
    {
        Assert.That(MajorScaleMode.Items.Count(), Is.EqualTo(7));
    }

    // Degree -> (mode name, tonal-centre pitch class) for the C-major parent scale.
    [TestCase(1, "Ionian", 0)]   // C
    [TestCase(2, "Dorian", 2)]   // D
    [TestCase(3, "Phrygian", 4)] // E
    [TestCase(4, "Lydian", 5)]   // F
    [TestCase(5, "Mixolydian", 7)] // G
    [TestCase(6, "Aeolian", 9)]  // A
    [TestCase(7, "Locrian", 11)] // B
    public void Mode_HasExpectedNameAndTonalCentre(int degree, string expectedName, int expectedTonalCentrePc)
    {
        var mode = MajorScaleMode.Get(degree);

        Assert.Multiple(() =>
        {
            Assert.That(mode.Name, Is.EqualTo(expectedName));
            Assert.That(mode.TonalCenter.PitchClass.Value, Is.EqualTo(expectedTonalCentrePc));
            Assert.That(mode.Notes.Count, Is.EqualTo(7));
            Assert.That(mode.ParentScale.PitchClassSet.Id, Is.EqualTo(Scale.Major.PitchClassSet.Id));
        });
    }

    // Dorian, Phrygian, Aeolian and Locrian carry a minor third; Ionian, Lydian, Mixolydian are major.
    [TestCase(1, false)] // Ionian
    [TestCase(2, true)]  // Dorian
    [TestCase(3, true)]  // Phrygian
    [TestCase(4, false)] // Lydian
    [TestCase(5, false)] // Mixolydian
    [TestCase(6, true)]  // Aeolian
    [TestCase(7, true)]  // Locrian
    public void Mode_IsMinorMode_TracksMinorThird(int degree, bool expectedMinor)
    {
        Assert.That(MajorScaleMode.Get(degree).IsMinorMode, Is.EqualTo(expectedMinor));
    }

    [Test]
    public void Get_ByDegreeValueObject_EqualsGetByInt()
    {
        var byObject = MajorScaleMode.Get(MajorScaleDegree.Dorian);
        var byInt = MajorScaleMode.Get(2);

        Assert.That(byObject.Name, Is.EqualTo(byInt.Name));
    }

    [Test]
    public void AllModes_ShareParentScaleIntervalClassVector()
    {
        // Modal invariance: every rotation of the diatonic scale has the same interval-class vector.
        foreach (var mode in MajorScaleMode.Items)
        {
            Assert.That(mode.ParentScale.IntervalClassVector, Is.EqualTo(IntervalClassVector.Major),
                $"Mode {mode.Name} does not share the diatonic interval-class vector");
        }
    }
}
