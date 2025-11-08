namespace GA.Business.Core.Tests.Tonal;

using Core.Atonal;
using Core.Notes;
using Scales;

[TestFixture]
public class ModalFamilyScaleModeTests
{
    [Test]
    public void Basic()
    {
        // var aa = ModalFamily.Items.First(f => f.IntervalClassVector == IntervalClassVector.Parse("<2 5 4 3 6 1>"));
        var majorModalFamily = ModalFamily.Major;
        var modes = ModalFamilyScaleModeFactory.CreateModesFromFamily(majorModalFamily).ToList();
    }

    [Test]
    public void LydianMode_HasCorrectProperties()
    {
        // Arrange
        var majorScale = Scale.Major; // C major scale by default
        var lydianDegree = 4; // Lydian is the 4th mode
        var lydianMode = ModalFamilyScaleMode.FromScale(majorScale, lydianDegree);

        // Assert
        Assert.That(lydianMode, Is.Not.Null);
        Assert.Multiple(() =>
        {
            // Check basic properties
            Assert.That(lydianMode!.Degree, Is.EqualTo(lydianDegree));
            Assert.That(lydianMode.Name, Is.EqualTo("Mode 4 of 7 notes - <2 5 4 3 6 1> (7 items)"));
            Assert.That(lydianMode.IsMinorMode, Is.False);

            // Check notes (F Lydian: F G A B C D E)
            var expectedNotes = AccidentedNoteCollection.Parse("F G A B C D E");
            Assert.That(lydianMode.Notes, Is.EqualTo(expectedNotes));

            // Check intervals from root
            var expectedIntervals = DiatonicIntervalCollection.Parse("P1 M2 M3 A4 P5 M6 M7");
            Assert.That(lydianMode.SimpleIntervals, Is.EqualTo(expectedIntervals));

            // Check characteristic intervals (what makes Lydian unique)
            var expectedCharacteristicNotes = AccidentedNoteCollection.Parse("B");
            Assert.That(lydianMode.CharacteristicNotes, Is.EqualTo(expectedCharacteristicNotes));

            // Check modal family properties
            Assert.That(lydianMode.ModalFamily.IntervalClassVector.ToString(), Is.EqualTo("<2 5 4 3 6 1>"));
            Assert.That(lydianMode.ModalFamily.NoteCount, Is.EqualTo(7));

            // Check reference mode (Ionian for major modes)
            Assert.That(lydianMode.RefMode.Notes,
                Is.EqualTo(AccidentedNoteCollection.Parse("C D E F G A B"))); // C Ionian
        });
    }
}
