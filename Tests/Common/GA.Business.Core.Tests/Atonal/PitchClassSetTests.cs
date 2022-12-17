namespace GA.Business.Core.Tests.Atonal;

using NUnit.Framework;

using GA.Business.Core.Notes;
using GA.Business.Core.Atonal;

public class PitchClassSetTests
{
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_NormalOrder()
    {
        // https://learnmusictheory.net/PDFs/pdffiles/06-10-SetTheorySimplified.pdf
        // Assert.AreEqual(Interval.Simple.P1, Note.Chromatic.C.GetInterval(Note.Chromatic.C));
    }
}