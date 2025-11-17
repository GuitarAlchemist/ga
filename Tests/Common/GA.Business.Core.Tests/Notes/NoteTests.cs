namespace GA.Business.Core.Tests.Notes;

using Core.Notes;
using Core.Notes.Extensions;
using Core.Notes.Primitives;

[TestFixture]
public class NoteTests
{
    public static IEnumerable<TestCaseData> GetIntervalTestCases
    {
        get
        {
            yield return new TestCaseData(Note.Chromatic.C, Note.Chromatic.C, Interval.Simple.P1);
            yield return new TestCaseData(Note.Chromatic.C, Note.Chromatic.D, Interval.Simple.M2);
            yield return new TestCaseData(Note.Chromatic.C, Note.Chromatic.E, Interval.Simple.M3);
            yield return new TestCaseData(Note.Chromatic.C, Note.Chromatic.F, Interval.Simple.P4);
            yield return new TestCaseData(Note.Chromatic.C, Note.Chromatic.G, Interval.Simple.P5);
            yield return new TestCaseData(Note.Chromatic.C, Note.Chromatic.A, Interval.Simple.M6);
            yield return new TestCaseData(Note.Chromatic.C, Note.Chromatic.B, Interval.Simple.M7);
        }
    }

    [Test]
    [TestCaseSource(nameof(GetIntervalTestCases))]
    public void Test_Chromatic_GetInterval(Note.Chromatic startingNote, Note.Chromatic endingNote,
        Interval.Simple expectedInterval)
    {
        var actualInterval = startingNote.GetInterval(endingNote);

        Assert.That(actualInterval, Is.EqualTo(expectedInterval));
    }

    [Test(TestOf = typeof(Note.Accidented))]
    public void Test_2()
    {
        var key = Key.Major.F;
        var keyRoot = key.Root;
        var keynotes = key.Notes;

        var f = new Note.Accidented(NaturalNote.F);
        var g = new Note.Accidented(NaturalNote.G);
        var a = new Note.Accidented(NaturalNote.A);
        var b = new Note.Accidented(NaturalNote.B);
        var c = new Note.Accidented(NaturalNote.C);
        var d = new Note.Accidented(NaturalNote.D);
        var e = new Note.Accidented(NaturalNote.E);

        var i1 = f.GetInterval(f);
        var i2 = f.GetInterval(g);
        var i3 = f.GetInterval(a);
        var i4 = f.GetInterval(b);
        var i5 = f.GetInterval(c);
        var i6 = f.GetInterval(d);
        var i7 = f.GetInterval(e);
    }
}
