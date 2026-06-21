namespace GA.Domain.Core.Tests.Theory.Tonal;

using System.Linq;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;
using NUnit.Framework;

/// <summary>
///     Characterization tests for <see cref="KeySignature" /> — the [-7, +7] sharp/flat count and the
///     circle-of-fifths/fourths derivation of its accidented notes.
/// </summary>
[TestFixture]
public class KeySignatureTests
{
    [Test]
    public void Items_SpanFifteenSignatures()
    {
        // -7 (7 flats) through +7 (7 sharps), inclusive.
        Assert.That(KeySignature.Items.Count, Is.EqualTo(15));
    }

    [TestCase(0, 0)]
    [TestCase(3, 3)]
    [TestCase(-2, 2)]
    [TestCase(7, 7)]
    [TestCase(-7, 7)]
    public void AccidentalCount_IsAbsoluteValue(int value, int expectedCount)
    {
        Assert.That(((KeySignature)value).AccidentalCount, Is.EqualTo(expectedCount));
    }

    [TestCase(0, AccidentalKind.Sharp)]  // C major: no accidentals, classified sharp-side
    [TestCase(4, AccidentalKind.Sharp)]
    [TestCase(-1, AccidentalKind.Flat)]
    [TestCase(-5, AccidentalKind.Flat)]
    public void AccidentalKind_TracksSign(int value, AccidentalKind expectedKind)
    {
        Assert.That(((KeySignature)value).AccidentalKind, Is.EqualTo(expectedKind));
    }

    [Test]
    public void SharpAndFlat_FactoriesProduceSignedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KeySignature.Sharp(3).Value, Is.EqualTo(3));
            Assert.That(KeySignature.Flat(2).Value, Is.EqualTo(-2));
            Assert.That(KeySignature.Sharp(3).IsSharpKey, Is.True);
            Assert.That(KeySignature.Flat(2).IsFlatKey, Is.True);
        });
    }

    [Test]
    public void AccidentedNotes_CountMatchesAccidentalCount()
    {
        foreach (var ks in KeySignature.Items)
        {
            Assert.That(ks.AccidentedNotes.Count, Is.EqualTo(ks.AccidentalCount),
                $"Key signature {ks.Value} accidented-note count mismatch");
        }
    }

    [Test]
    public void FirstSharp_IsFSharp_FirstFlat_IsBFlat()
    {
        // Circle of fifths begins on F# for sharps; circle of fourths begins on Bb for flats.
        Assert.Multiple(() =>
        {
            Assert.That(KeySignature.Sharp(1).AccidentedNotes.Single().PitchClass.Value, Is.EqualTo(6));  // F#
            Assert.That(KeySignature.Flat(1).AccidentedNotes.Single().PitchClass.Value, Is.EqualTo(10)); // Bb
        });
    }
}
