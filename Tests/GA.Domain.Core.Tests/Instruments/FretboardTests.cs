namespace GA.Domain.Core.Tests.Instruments;

using System;
using System.Linq;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using NUnit.Framework;

/// <summary>
///     Characterization tests for <see cref="Fretboard" /> — geometry, the string/fret -> note mapping
///     (string index 0 = Str 1 = high E in standard tuning), and bounds checking.
/// </summary>
[TestFixture]
public class FretboardTests
{
    [Test]
    public void Default_IsSixStringTwentyFourFretGuitar()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Fretboard.Default.StringCount, Is.EqualTo(6));
            Assert.That(Fretboard.Default.FretCount, Is.EqualTo(24));
            Assert.That(Fretboard.Default.StringCount, Is.EqualTo(Fretboard.Default.Tuning.StringCount));
        });
    }

    [Test]
    public void CreateGuitar_HonoursRequestedFretCount()
    {
        Assert.That(Fretboard.CreateGuitar(12).FretCount, Is.EqualTo(12));
    }

    // Open-string pitch classes for standard tuning, indexed 0 (high E) .. 5 (low E).
    [TestCase(0, 4)]  // E
    [TestCase(1, 11)] // B
    [TestCase(2, 7)]  // G
    [TestCase(3, 2)]  // D
    [TestCase(4, 9)]  // A
    [TestCase(5, 4)]  // E
    public void GetNote_OpenString_HasExpectedPitchClass(int stringIndex, int expectedPc)
    {
        Assert.That(Fretboard.Default.GetNote(stringIndex, 0).PitchClass.Value, Is.EqualTo(expectedPc));
    }

    [Test]
    public void GetNote_TwelfthFret_IsSamePitchClassAsOpen()
    {
        // True under both the correct semantics and the current implementation (12 frets = one octave).
        var open = Fretboard.Default.GetNote(0, 0).PitchClass.Value;
        var octave = Fretboard.Default.GetNote(0, 12).PitchClass.Value;
        Assert.That(octave, Is.EqualTo(open));
    }

    // High E string (index 0), fret -> pitch class: each fret raises the pitch one semitone.
    [TestCase(0, 4)]  // E (open)
    [TestCase(1, 5)]  // F
    [TestCase(2, 6)]  // F#
    [TestCase(3, 7)]  // G
    [TestCase(5, 9)]  // A
    [TestCase(12, 4)] // E (one octave up)
    public void GetNote_AppliesFretAsSemitoneOffset(int fret, int expectedPc)
    {
        Assert.That(Fretboard.Default.GetNote(0, fret).PitchClass.Value, Is.EqualTo(expectedPc));
    }

    [Test]
    public void GetPitchClass_AgreesWithGetNote()
    {
        Assert.That(Fretboard.Default.GetPitchClass(3, 5), Is.EqualTo(Fretboard.Default.GetNote(3, 5).PitchClass));
    }

    [TestCase(-1, 0)]  // string below range
    [TestCase(6, 0)]   // string == StringCount (out of range)
    [TestCase(0, -1)]  // fret below range
    [TestCase(0, 25)]  // fret above FretCount
    public void GetNote_OutOfRange_Throws(int stringIndex, int fret)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Fretboard.Default.GetNote(stringIndex, fret));
    }

    [Test]
    public void GetPositionsForNote_ReturnsOnlyValidPositions()
    {
        var fretboard = Fretboard.CreateGuitar(12);
        var positions = fretboard.GetPositionsForNote(fretboard.GetNote(0, 0)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(positions, Is.Not.Empty);
            Assert.That(positions.All(fretboard.IsValidPosition), Is.True);
        });
    }

    // Notes are records of different types (Chromatic, Sharp, Flat, Accidented): a Sharp C never
    // equals the Chromatic C that GetNote returns, so matching must compare pitch classes.
    [Test]
    public void GetPositionsForNote_MatchesByPitchClass_WhateverTheNoteType()
    {
        var fretboard = Fretboard.CreateGuitar(12);
        string[] Locations(Note note) =>
            [.. fretboard.GetPositionsForNote(note).Select(p => $"{p.Location.Str.Value}:{p.Location.Fret.Value}")];

        var expected = Locations(Note.Chromatic.C);

        Assert.Multiple(() =>
        {
            // C on a 12-fret guitar: strings 1..6 at frets 8, 1, 5, 10, 3, 8
            Assert.That(expected, Is.EquivalentTo(new[] { "1:8", "2:1", "3:5", "4:10", "5:3", "6:8" }));
            Assert.That(Locations(Note.Sharp.C), Is.EqualTo(expected));
            Assert.That(Locations(Note.Flat.C), Is.EqualTo(expected));
            Assert.That(Locations(Note.Accidented.C), Is.EqualTo(expected));
            Assert.That(Locations(new Note.Accidented(NaturalNote.B, Accidental.Sharp)), Is.EqualTo(expected));
            Assert.That(fretboard.GetPositionsForNote(Note.Sharp.C).OfType<Position.Played>()
                .All(p => p.MidiNote.PitchClass.Value == 0), Is.True);
        });
    }
}
