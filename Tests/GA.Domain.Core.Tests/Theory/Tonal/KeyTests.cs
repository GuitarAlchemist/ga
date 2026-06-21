namespace GA.Domain.Core.Tests.Theory.Tonal;

using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;
using GA.Domain.Core.Theory.Tonal.Scales;
using NUnit.Framework;

/// <summary>
///     Characterization tests for <see cref="Key" /> (Major | Minor) — 15 keys per mode, key roots,
///     diatonic note content, and the relative-key relationship via shared key signatures.
/// </summary>
[TestFixture]
public class KeyTests
{
    [Test]
    public void Items_ContainThirtyKeys()
    {
        // 15 major + 15 minor.
        Assert.That(Key.Items.Count, Is.EqualTo(30));
        Assert.That(Key.Major.MajorItems.Count, Is.EqualTo(15));
        Assert.That(Key.Minor.MinorItems.Count, Is.EqualTo(15));
    }

    [TestCase(0, 0)]  // C
    [TestCase(1, 7)]  // G
    [TestCase(-1, 5)] // F
    [TestCase(2, 2)]  // D
    public void MajorKey_RootPitchClass_TracksSignature(int signature, int expectedRootPc)
    {
        var key = new Key.Major(signature);
        Assert.That(key.Root.PitchClass.Value, Is.EqualTo(expectedRootPc));
    }

    [Test]
    public void MajorKey_HasSevenNotes()
    {
        Assert.That(Key.Major.C.Notes.Count, Is.EqualTo(7));
    }

    [Test]
    public void CMajorKey_PitchClassSet_MatchesMajorScale()
    {
        Assert.That(Key.Major.C.PitchClassSet.Id, Is.EqualTo(Scale.Major.PitchClassSet.Id));
    }

    [Test]
    public void RelativeKeys_ShareSamePitchClassSet()
    {
        // C major (0 sharps/flats) and A minor (0 sharps/flats) are relative keys — same notes.
        Assert.That(Key.Minor.Am.PitchClassSet.Id, Is.EqualTo(Key.Major.C.PitchClassSet.Id));
    }

    [Test]
    public void KeyMode_IsModeSpecific()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Key.Major.C.KeyMode, Is.EqualTo(KeyMode.Major));
            Assert.That(Key.Minor.Am.KeyMode, Is.EqualTo(KeyMode.Minor));
        });
    }

    [Test]
    public void MajorKey_AccidentalKind_TracksSignature()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Key.Major.G.AccidentalKind, Is.EqualTo(AccidentalKind.Sharp));
            Assert.That(Key.Major.F.AccidentalKind, Is.EqualTo(AccidentalKind.Flat));
        });
    }

    [Test]
    public void TryParse_KnownRoot_ResolvesKey()
    {
        var ok = Key.Major.TryParse("G", out var g);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(g.Root.PitchClass.Value, Is.EqualTo(7));
            Assert.That(g.KeySignature.Value, Is.EqualTo(1));
        });
    }
}
