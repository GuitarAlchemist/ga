namespace GA.Domain.Core.Tests.Primitives;

using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Primitives;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using NUnit.Framework;

[TestFixture]
public class NoteTests
{
    public static IEnumerable<TestCaseData> GetIntervalTestCases
    {
        get
        {
            yield return new TestCaseData(new Note.Chromatic(PitchClass.C), new Note.Chromatic(PitchClass.C), Interval.Simple.P1);
            yield return new TestCaseData(new Note.Chromatic(PitchClass.C), new Note.Chromatic(PitchClass.D), Interval.Simple.M2);
            yield return new TestCaseData(new Note.Chromatic(PitchClass.C), new Note.Chromatic(PitchClass.E), Interval.Simple.M3);
            yield return new TestCaseData(new Note.Chromatic(PitchClass.C), new Note.Accidented(NaturalNote.F).ToChromatic(), Interval.Simple.P4);
            // new Note.Chromatic(PitchClass.F) is debatable because F is 5.
            yield return new TestCaseData(new Note.Chromatic(PitchClass.C), new Note.Chromatic(PitchClass.F), Interval.Simple.P4);
            yield return new TestCaseData(new Note.Chromatic(PitchClass.C), new Note.Chromatic(PitchClass.G), Interval.Simple.P5);
            yield return new TestCaseData(new Note.Chromatic(PitchClass.C), new Note.Chromatic(PitchClass.A), Interval.Simple.M6);
            yield return new TestCaseData(new Note.Chromatic(PitchClass.C), new Note.Chromatic(PitchClass.B), Interval.Simple.M7);
        }
    }
    [Test]
    [TestCaseSource(nameof(GetIntervalTestCases))]
    public void Test_Chromatic_GetInterval(Note.Chromatic startingNote, Note.Chromatic endingNote,
        Interval.Simple expectedInterval)
    {
        // Act
        var actualInterval = startingNote.GetInterval(endingNote);
        // Assert
        TestContext.WriteLine($"From {startingNote} to {endingNote} - Expected: {expectedInterval}, Actual: {actualInterval} (Standard chromatic interval distance)");
        Assert.That(actualInterval, Is.EqualTo(expectedInterval), $"Interval from {startingNote} to {endingNote} should be {expectedInterval}.");
    }
    [Test(TestOf = typeof(Note.Accidented))]
    public void Test_Accidented_GetInterval_F_to_Octave()
    {
        // Arrange
        var f = new Note.Accidented(NaturalNote.F);
        var g = new Note.Accidented(NaturalNote.G);
        var a = new Note.Accidented(NaturalNote.A);
        var b = new Note.Accidented(NaturalNote.B);
        var c = new Note.Accidented(NaturalNote.C);
        var d = new Note.Accidented(NaturalNote.D);
        var e = new Note.Accidented(NaturalNote.E);
        // Act
        var i1 = f.GetInterval(f);
        var i2 = f.GetInterval(g);
        var i3 = f.GetInterval(a);
        var i4 = f.GetInterval(b);
        var i5 = f.GetInterval(c);
        var i6 = f.GetInterval(d);
        var i7 = f.GetInterval(e);
        // Assert
        TestContext.WriteLine($"F to F: {i1}, F to G: {i2}, F to A: {i3}, F to B: {i4}, F to C: {i5}, F to D: {i6}, F to E: {i7}");
        Assert.Multiple(() =>
        {
            Assert.That(i1, Is.EqualTo(Interval.Simple.P1));
            Assert.That(i2, Is.EqualTo(Interval.Simple.M2));
            Assert.That(i3, Is.EqualTo(Interval.Simple.M3));
            Assert.That(i4, Is.EqualTo(new Interval.Simple { Size = SimpleIntervalSize.Fourth, Quality = IntervalQuality.Augmented })); // F to B is Aug 4th
            Assert.That(i5, Is.EqualTo(Interval.Simple.P5));
            Assert.That(i6, Is.EqualTo(Interval.Simple.M6));
            Assert.That(i7, Is.EqualTo(Interval.Simple.M7));
        });
    }
}
