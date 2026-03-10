namespace GA.Business.Core.Tests.Fretboard.Voicings;

using Domain.Core.Instruments.Fretboard.Voicings.Core;
using Domain.Core.Instruments.Positions;
using Domain.Core.Instruments.Primitives;
using Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Voicings.Analysis;

[TestFixture]
public class VoicingValidatorTests
{
    [Test]
    public void IsPhysicallyValid_CMajorOpenVoicing_ShouldReturnTrue()
    {
        // Arrange: C major open chord — each note on a distinct string (x32010 shape)
        var voicing = CreatePlayedVoicing(
            (5, 3, 48),
            (4, 2, 52),
            (3, 0, 55),
            (2, 1, 60),
            (1, 0, 64));

        Assert.That(VoicingValidator.IsPhysicallyValid(voicing), Is.True);
    }

    [Test]
    public void HasDuplicateStrings_TwoNotesOnSameString_ShouldReturnTrue()
    {
        // Arrange: two played notes both on string 1 (physically impossible)
        var voicing = CreatePlayedVoicing(
            (1, 0, 64),
            (1, 3, 67),
            (2, 1, 72));

        Assert.That(VoicingValidator.HasDuplicateStrings(voicing), Is.True);
        Assert.That(VoicingValidator.IsPhysicallyValid(voicing), Is.False);
    }

    [Test]
    public void ThrowIfInvalid_DuplicateStringVoicing_ShouldThrowInvalidOperationException()
    {
        var voicing = CreatePlayedVoicing(
            (1, 0, 64),
            (1, 3, 67));

        Assert.Throws<InvalidOperationException>(() => VoicingValidator.ThrowIfInvalid(voicing));
    }

    [Test]
    public void ThrowIfInvalid_ValidVoicing_ShouldNotThrow()
    {
        var voicing = CreatePlayedVoicing(
            (1, 0, 64),
            (2, 1, 60),
            (3, 0, 55));

        Assert.DoesNotThrow(() => VoicingValidator.ThrowIfInvalid(voicing));
    }

    [Test]
    public void HasDuplicateStrings_MutedStrings_AreNotCountedAsDuplicates()
    {
        // Arrange: string 6 muted, strings 1–5 played on distinct strings → valid
        var positions = new Position[]
        {
            new Position.Muted(new Str(6)),
            new Position.Played(new PositionLocation(new Str(5), new Fret(3)), new MidiNote(48)),
            new Position.Played(new PositionLocation(new Str(4), new Fret(2)), new MidiNote(52)),
            new Position.Played(new PositionLocation(new Str(3), new Fret(0)), new MidiNote(55)),
            new Position.Played(new PositionLocation(new Str(2), new Fret(1)), new MidiNote(60)),
            new Position.Played(new PositionLocation(new Str(1), new Fret(0)), new MidiNote(64)),
        };
        var voicing = new Voicing(positions, [new(48), new(52), new(55), new(60), new(64)]);

        Assert.That(VoicingValidator.HasDuplicateStrings(voicing), Is.False);
        Assert.That(VoicingValidator.IsPhysicallyValid(voicing), Is.True);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Voicing CreatePlayedVoicing(params (int str, int fret, int midi)[] notes)
    {
        var positions = notes
            .Select(n => (Position)new Position.Played(
                new PositionLocation(new Str(n.str), new Fret(n.fret)),
                new MidiNote(n.midi)))
            .ToArray();
        var midiNotes = notes.Select(n => new MidiNote(n.midi)).ToArray();
        return new Voicing(positions, midiNotes);
    }
}
