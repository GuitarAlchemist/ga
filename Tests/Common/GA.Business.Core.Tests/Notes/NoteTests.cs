using GA.Business.Core.Notes.Extensions;
using GA.Business.Core.Notes.Primitives;

namespace GA.Business.Core.Tests.Notes;

using NUnit.Framework;

using Intervals;
using GA.Business.Core.Notes;

public class NoteTests
{
    [Test(TestOf = typeof(Note.Chromatic))]
    public void Test_Chromatic_GetInterval()
    {
        Assert.AreEqual((Interval.Chromatic) 0, Note.Chromatic.C.GetInterval(Note.Chromatic.C));
        Assert.AreEqual((Interval.Chromatic) 2, Note.Chromatic.C.GetInterval(Note.Chromatic.D));
        Assert.AreEqual((Interval.Chromatic) 4, Note.Chromatic.C.GetInterval(Note.Chromatic.E));
        Assert.AreEqual((Interval.Chromatic) 5, Note.Chromatic.C.GetInterval(Note.Chromatic.F));
        Assert.AreEqual((Interval.Chromatic) 7, Note.Chromatic.C.GetInterval(Note.Chromatic.G));
        Assert.AreEqual((Interval.Chromatic) 9, Note.Chromatic.C.GetInterval(Note.Chromatic.A));
        Assert.AreEqual((Interval.Chromatic) 11, Note.Chromatic.C.GetInterval(Note.Chromatic.B));
    }

    [Test(TestOf = typeof(Note.AccidentedNote))]
    public void Test_2()
    {
        var fSharp = new Note.AccidentedNote(NaturalNote.F, Accidental.Sharp);
        var bFlat = new Note.AccidentedNote(NaturalNote.C, Accidental.Flat);

        var i = fSharp.GetInterval(bFlat);
    }
}
