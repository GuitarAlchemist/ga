namespace GA.Domain.Core.Tests.Primitives;

using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;

/// <summary>
///     MIDI numbers follow scientific pitch notation: the octave number changes at C and middle C (C4) is 60,
///     so a spelling that crosses the B/C boundary belongs to the neighbouring octave's MIDI range
///     (Cb4 sounds as B3 = 59, B#3 sounds as C4 = 60).
/// </summary>
[TestFixture]
public class PitchMidiNoteTests
{
    private static IEnumerable<TestCaseData> MidiNoteCases
    {
        get
        {
            yield return new TestCaseData(new Pitch.Flat(Note.Flat.C, 4), 60).SetName("C4 is 60");
            yield return new TestCaseData(new Pitch.Flat(Note.Flat.AFlat, 4), 68).SetName("Ab4 is 68");
            yield return new TestCaseData(new Pitch.Flat(Note.Flat.CFlat, 4), 59).SetName("Cb4 is 59 (B3)");
            yield return new TestCaseData(new Pitch.Flat(new Note.Flat(NaturalNote.C, FlatAccidental.DoubleFlat), 4), 58).SetName("Cbb4 is 58 (Bb3)");
            yield return new TestCaseData(new Pitch.Flat(Note.Flat.FFlat, 4), 64).SetName("Fb4 is 64 (E4)");
            yield return new TestCaseData(new Pitch.Sharp(Note.Sharp.B, 3), 59).SetName("B3 is 59");
            yield return new TestCaseData(new Pitch.Sharp(new Note.Sharp(NaturalNote.B, SharpAccidental.Sharp), 3), 60).SetName("B#3 is 60 (C4)");
            yield return new TestCaseData(new Pitch.Sharp(new Note.Sharp(NaturalNote.B, SharpAccidental.DoubleSharp), 3), 61).SetName("B##3 is 61 (C#4)");
            yield return new TestCaseData(new Pitch.Sharp(new Note.Sharp(NaturalNote.E, SharpAccidental.Sharp), 4), 65).SetName("E#4 is 65 (F4)");
            yield return new TestCaseData(new Pitch.Chromatic(new Note.Chromatic(PitchClass.B), 3), 59).SetName("Chromatic B3 is 59");
        }
    }

    [TestCaseSource(nameof(MidiNoteCases))]
    public void MidiNote_FollowsScientificPitchNotation(Pitch pitch, int expected) =>
        Assert.That(pitch.MidiNote.Value, Is.EqualTo(expected));

    [Test]
    public void CompareTo_OrdersEnharmonicSpellingsAcrossTheOctaveBoundaryBySound()
    {
        var cFlat4 = new Pitch.Flat(Note.Flat.CFlat, 4);
        var bSharp3 = new Pitch.Sharp(new Note.Sharp(NaturalNote.B, SharpAccidental.Sharp), 3);

        Assert.Multiple(() =>
        {
            Assert.That(cFlat4 < new Pitch.Flat(Note.Flat.C, 4), Is.True, "Cb4 is below C4");
            Assert.That(bSharp3 > new Pitch.Sharp(Note.Sharp.B, 3), Is.True, "B#3 is above B3");
            Assert.That(cFlat4.CompareTo(new Pitch.Sharp(Note.Sharp.B, 3)), Is.Zero, "Cb4 and B3 sound the same");
        });
    }
}
