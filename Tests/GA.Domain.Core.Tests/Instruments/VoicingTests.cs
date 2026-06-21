namespace GA.Domain.Core.Tests.Instruments;

using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using NUnit.Framework;

/// <summary>
///     Characterization tests for <see cref="Voicing" /> — the diagram-based identity and the derived
///     fret-span / played-count / barre analysis read off the position list.
/// </summary>
[TestFixture]
public class VoicingTests
{
    private static Position Played(int str, int fret) =>
        new Position.Played(new PositionLocation((Str)str, (Fret)fret), (MidiNote)40);

    private static Position Muted(int str) => new Position.Muted((Str)str);

    private static Voicing Make(params Position[] positions) => new(positions, []);

    [Test]
    public void Diagram_RendersFretsAndMutes()
    {
        var voicing = Make(Played(1, 0), Played(2, 2), Muted(3));
        Assert.That(voicing.Diagram, Is.EqualTo("0-2-x"));
    }

    [Test]
    public void PlayedNoteCount_ExcludesMutedStrings()
    {
        var voicing = Make(Played(1, 0), Played(2, 2), Muted(3));
        Assert.That(voicing.PlayedNoteCount, Is.EqualTo(2));
    }

    [Test]
    public void FretSpan_MeasuresAcrossFrettedNotesOnly()
    {
        // Frets 2 and 5 are fretted (open/0 ignored): span = 5 - 2 = 3.
        var voicing = Make(Played(1, 0), Played(2, 2), Played(3, 5));
        Assert.Multiple(() =>
        {
            Assert.That(voicing.FretSpan, Is.EqualTo(3));
            Assert.That(voicing.MinFret, Is.EqualTo(2));
            Assert.That(voicing.MaxFret, Is.EqualTo(5));
        });
    }

    [Test]
    public void FretSpan_AllOpenOrMuted_IsZero()
    {
        var voicing = Make(Played(1, 0), Muted(2));
        Assert.Multiple(() =>
        {
            Assert.That(voicing.FretSpan, Is.EqualTo(0));
            Assert.That(voicing.MinFret, Is.Null);
            Assert.That(voicing.MaxFret, Is.EqualTo(0));
        });
    }

    [Test]
    public void HasBarre_TrueWhenThreeOrMoreShareAFret()
    {
        var barred = Make(Played(1, 3), Played(2, 3), Played(3, 3));
        var notBarred = Make(Played(1, 3), Played(2, 3), Played(3, 5));

        Assert.Multiple(() =>
        {
            Assert.That(barred.HasBarre(), Is.True);
            Assert.That(notBarred.HasBarre(), Is.False);
        });
    }

    [Test]
    public void Equality_IsBasedOnDiagram_NotNoteArrays()
    {
        var a = new Voicing([Played(1, 0), Played(2, 2)], [(MidiNote)40, (MidiNote)45]);
        var b = new Voicing([Played(1, 0), Played(2, 2)], [(MidiNote)52, (MidiNote)57]);

        Assert.Multiple(() =>
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void Equality_DifferentDiagrams_AreNotEqual()
    {
        var a = Make(Played(1, 0), Played(2, 2));
        var b = Make(Played(1, 0), Played(2, 3));
        Assert.That(a, Is.Not.EqualTo(b));
    }
}
