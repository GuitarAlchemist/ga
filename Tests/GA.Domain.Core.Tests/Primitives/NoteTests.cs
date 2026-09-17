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

    [TestCase("B", "B", 11)]
    [TestCase("b", "B", 11)]
    [TestCase(" E ", "E", 4)]
    [TestCase("Bb", "Bb", 10)]
    [TestCase("BB", "Bb", 10)]
    [TestCase("B♭", "Bb", 10)]
    [TestCase("Ebb", "Ebb", 2)]
    public void Flat_TryParse_Valid(string input, string expected, int expectedPitchClass)
    {
        var parsed = Note.Flat.TryParse(input, null, out var note);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(note.ToString(), Is.EqualTo(expected));
            Assert.That(note.PitchClass.Value, Is.EqualTo(expectedPitchClass));
        });
    }

    [TestCase("")]
    [TestCase("H")]
    [TestCase("E#")]
    [TestCase("Bbbbb")]
    [TestCase("Bb#")]
    [TestCase("Blob")]
    public void Flat_TryParse_Invalid(string input) =>
        Assert.That(Note.Flat.TryParse(input, null, out _), Is.False);

    [TestCase("C", "C", 0)]
    [TestCase("C#", "C#", 1)]
    [TestCase("c♯", "C#", 1)]
    [TestCase("C##", "Cx", 2)]
    [TestCase("Cx", "Cx", 2)]
    public void Sharp_TryParse_Valid(string input, string expected, int expectedPitchClass)
    {
        var parsed = Note.Sharp.TryParse(input, null, out var note);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(note.ToString(), Is.EqualTo(expected));
            Assert.That(note.PitchClass.Value, Is.EqualTo(expectedPitchClass));
        });
    }

    [TestCase("")]
    [TestCase("Cb")]
    [TestCase("C###")]
    [TestCase("C#b")]
    [TestCase("Cfoo#")]
    [TestCase("Hello#")]
    public void Sharp_TryParse_Invalid(string input) =>
        Assert.That(Note.Sharp.TryParse(input, null, out _), Is.False);

    [Test]
    public void KeyNotes_RoundTripThroughToString() =>
        Assert.Multiple(() =>
        {
            foreach (var note in Note.Sharp.Items)
            {
                Assert.That(Note.Sharp.Parse(note.ToString(), null), Is.EqualTo(note));
            }

            foreach (var note in Note.Flat.Items)
            {
                Assert.That(Note.Flat.Parse(note.ToString(), null), Is.EqualTo(note));
            }
        });

    // Regression, found by the music-theory-ga course: the flat sign counts only
    // after the letter, so "B" is B natural while "bb" and "E♭" are flats.
    [TestCase("Bb", 10)]
    [TestCase("bb", 10)]
    [TestCase("E♭", 3)]
    [TestCase("eb", 3)]
    [TestCase("B", 11)]
    [TestCase("F", 5)]
    public void Test_Flat_Parse_ReadsTheFlatSignOnlyAfterTheLetter(string text, int pitchClass) =>
        Assert.That(Note.Flat.Parse(text, null).PitchClass.Value, Is.EqualTo(pitchClass));
}
