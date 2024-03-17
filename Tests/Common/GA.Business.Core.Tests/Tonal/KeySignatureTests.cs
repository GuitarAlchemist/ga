namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class KeySignatureTests
{
    // Use TestCase attributes to specify each key signature and its expected notes.
    [TestCase(-7, new[] { "Bbm", "Ebm", "Abm", "Db", "Gb", "Cb", "Fb" })]
    [TestCase(-6, new[] { "Bbm", "Ebm", "Abm", "Db", "Gb", "Cb" })]
    [TestCase(-5, new[] { "Bbm", "Ebm", "Abm", "Db", "Gb" })]
    [TestCase(-4, new[] { "Bbm", "Ebm", "Abm", "Db" })]
    [TestCase(-3, new[] { "Bbm", "Ebm", "Abm" })]
    [TestCase(-2, new[] { "Bbm", "Ebm" })]
    [TestCase(-1, new[] { "Bbm" })]
    [TestCase(0, new string[] { })]
    [TestCase(1, new[] { "Fm#" })]
    [TestCase(2, new[] { "Fm#", "Cm#" })]
    [TestCase(3, new[] { "Fm#", "Cm#", "Gm#" })]
    [TestCase(4, new[] { "Fm#", "Cm#", "Gm#", "Dm#" })]
    [TestCase(5, new[] { "Fm#", "Cm#", "Gm#", "Dm#", "Am#" })]
    [TestCase(6, new[] { "Fm#", "Cm#", "Gm#", "Dm#", "Am#", "Em#" })]
    [TestCase(7, new[] { "Fm#", "Cm#", "Gm#", "Dm#", "Am#", "Em#", "B#" })]
    public void SignatureNotes_CorrectOrderAndAccidentals(int value, string[] expectedNotes)
    {
        var keySignature = KeySignature.FromValue(value);

        Assert.AreEqual(expectedNotes, keySignature.SignatureNotes.Select(note => note.ToString()).ToArray());
    }
}