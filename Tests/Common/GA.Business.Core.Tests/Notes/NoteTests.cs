using GA.Business.Core.Notes.Extensions;
using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.Tonal;

namespace GA.Business.Core.Tests.Notes;

using NUnit.Framework;

using Intervals;
using GA.Business.Core.Notes;

public class NoteTests
{
    [Test(TestOf = typeof(Note.Chromatic))]
    public void Test_Chromatic_GetInterval()
    {
        Assert.AreEqual((Interval.Chromatic)0, Note.Chromatic.C.GetInterval(Note.Chromatic.C));
        Assert.AreEqual((Interval.Chromatic)2, Note.Chromatic.C.GetInterval(Note.Chromatic.D));
        Assert.AreEqual((Interval.Chromatic)4, Note.Chromatic.C.GetInterval(Note.Chromatic.E));
        Assert.AreEqual((Interval.Chromatic)5, Note.Chromatic.C.GetInterval(Note.Chromatic.F));
        Assert.AreEqual((Interval.Chromatic)7, Note.Chromatic.C.GetInterval(Note.Chromatic.G));
        Assert.AreEqual((Interval.Chromatic)9, Note.Chromatic.C.GetInterval(Note.Chromatic.A));
        Assert.AreEqual((Interval.Chromatic)11, Note.Chromatic.C.GetInterval(Note.Chromatic.B));
    }

    [Test(TestOf = typeof(Note.AccidentedNote))]
    public void Test_2()
    {
        var key = Key.Major.F;
        var keyRoot = key.Root;
        var keynotes = key.GetNotes();

        var f = new Note.AccidentedNote(NaturalNote.F);
        var g = new Note.AccidentedNote(NaturalNote.G);
        var a = new Note.AccidentedNote(NaturalNote.A);
        var b = new Note.AccidentedNote(NaturalNote.B);
        var c = new Note.AccidentedNote(NaturalNote.C);
        var d = new Note.AccidentedNote(NaturalNote.D);
        var e = new Note.AccidentedNote(NaturalNote.E);


        var i1 = f.GetInterval(f);
        var i2 = f.GetInterval(g);
        var i3 = f.GetInterval(a);
        var i4 = f.GetInterval(b);
        var i5 = f.GetInterval(c);
        var i6 = f.GetInterval(d);
        var i7 = f.GetInterval(e);
    }
}
