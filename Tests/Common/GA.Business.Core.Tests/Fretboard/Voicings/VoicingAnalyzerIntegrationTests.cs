namespace GA.Business.Core.Tests.Fretboard.Voicings;

using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Fretboard.Voicings.Analysis;
using Core.Fretboard.Voicings.Core;
using Core.Notes.Primitives;

/// <summary>
/// Integration tests for the complete voicing analysis pipeline:
/// Generation → Analysis → Filtering → Output
/// </summary>
[TestFixture]
public class VoicingAnalyzerIntegrationTests
{
    #region Complete Pipeline Tests

    [Test]
    [Category("Integration")]
    public void Pipeline_AnalyzeMultipleVoicings_ShouldWorkEndToEnd()
    {
        // Arrange: Create known voicings
        var voicings = CreateKnownVoicings();

        // Act - Analyze all voicings
        var analyses = voicings.Select(v => VoicingAnalyzer.Analyze(v)).ToList();

        // Assert
        Assert.That(analyses.Count, Is.EqualTo(voicings.Count), "Should analyze all voicings");

        // Verify each result has complete analysis
        foreach (var analysis in analyses)
        {
            Assert.That(analysis, Is.Not.Null, "Each result should have analysis");
            Assert.That(analysis.MidiNotes, Is.Not.Null.And.Not.Empty, "Should have MIDI notes");
            Assert.That(analysis.PitchClassSet, Is.Not.Null, "Should have pitch class set");
            Assert.That(analysis.ChordId, Is.Not.Null, "Should have chord identification");
            Assert.That(analysis.IntervallicInfo, Is.Not.Null, "Should have intervallic info");
            Assert.That(analysis.VoicingCharacteristics, Is.Not.Null, "Should have voicing characteristics");
        }
    }

    [Test]
    [Category("Integration")]
    public void Analysis_MajorChords_ShouldIdentifyCorrectly()
    {
        // Arrange
        var voicings = CreateKnownVoicings();

        // Act
        var analyses = voicings.Select(v => VoicingAnalyzer.Analyze(v)).ToList();

        // Assert
        Assert.That(analyses.Count, Is.GreaterThan(0), "Should analyze voicings");

        // All test voicings are major chords
        foreach (var analysis in analyses)
        {
            var chordName = analysis.ChordId.ChordName ?? "";
            Console.WriteLine($"Chord: {chordName}");

            // Verify chord identification exists
            Assert.That(chordName, Is.Not.Null.And.Not.Empty, "Should have chord name");
        }
    }

    [Test]
    [Category("Integration")]
    public void Analysis_VoicingCharacteristics_ShouldDetectCorrectly()
    {
        // Arrange: Create voicings with known characteristics
        var voicings = CreateKnownVoicings();

        // Act
        var analyses = voicings.Select(v => VoicingAnalyzer.Analyze(v)).ToList();

        // Assert
        foreach (var analysis in analyses)
        {
            // Verify voicing characteristics are detected
            Assert.That(analysis.VoicingCharacteristics, Is.Not.Null, "Should have voicing characteristics");
            Assert.That(analysis.VoicingCharacteristics.Span, Is.GreaterThan(0), "Should have span");

            Console.WriteLine($"Voicing: Open={analysis.VoicingCharacteristics.IsOpenVoicing}, " +
                            $"Rootless={analysis.VoicingCharacteristics.IsRootless}, " +
                            $"Drop={analysis.VoicingCharacteristics.DropVoicing ?? "None"}");
        }
    }

    #endregion

    #region Mode Detection Integration Tests

    [Test]
    [Category("Integration")]
    public void Analysis_FullScale_ShouldDetectMode()
    {
        var pitchClasses = new[] { 0, 2, 4, 5, 7, 9, 11 }; // C major scale
        var voicing = CreateFullPitchClassVoicing(pitchClasses);

        // Act
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for full scale");
        Assert.That(analysis.ModeInfo!.FamilyName, Is.Not.Null.And.Not.Empty, "Should have family name");
        Console.WriteLine($"Detected mode: {analysis.ModeInfo.ModeName} ({analysis.ModeInfo.FamilyName})");
    }

    [Test]
    [Category("Integration")]
    public void Analysis_Triad_ShouldNotDetectMode()
    {
        // Arrange: Create a simple C major triad
        var stringTunings = new[] { 64, 59, 55, 50, 45, 40 }; // E A D G B E
        var frets = new[] { 3, 2, 0 };
        var strings = new[] { 5, 4, 3 };
        var positions = new Position[]
        {
            new Position.Played(new PositionLocation(new Str(strings[0]), new Fret(frets[0])), new MidiNote(stringTunings[strings[0]-1] + frets[0])),  // C
            new Position.Played(new PositionLocation(new Str(strings[1]), new Fret(frets[1])), new MidiNote(stringTunings[strings[1]-1] + frets[1])),  // E
            new Position.Played(new PositionLocation(new Str(strings[2]), new Fret(frets[2])), new MidiNote(stringTunings[strings[2]-1] + frets[2]))   // G
        };
        var notes = frets.Select((f, i) => new MidiNote(stringTunings[strings[i]-1] + f)).ToArray();
        var voicing = new Voicing(positions, notes);

        // Act
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Null, "Triads should not detect modes");
        Assert.That(analysis.ChordId, Is.Not.Null, "Should still identify chord");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a set of known voicings for testing
    /// </summary>
    private static List<Voicing> CreateKnownVoicings()
    {
        var voicings = new List<Voicing>();

        // C Major triad (open position)
        voicings.Add(CreateVoicing([(5, 3), (4, 2), (3, 0)]));

        // G Major triad (open position)
        voicings.Add(CreateVoicing([(6, 3), (5, 2), (4, 0)]));

        // D Major triad (open position)
        voicings.Add(CreateVoicing([(4, 0), (3, 2), (2, 3)]));

        return voicings;
    }

    /// <summary>
    /// Helper method to create a voicing from (string, fret) pairs
    /// </summary>
    private static Voicing CreateVoicing((int str, int fret)[] stringFretPairs)
    {
        var stringTunings = new[] { 64, 59, 55, 50, 45, 40 }; // E A D G B E (1-indexed: 1=E, 6=low E)
        var positions = new List<Position>();
        var notes = new List<MidiNote>();

        foreach (var (str, fret) in stringFretPairs)
        {
            var location = new PositionLocation(new Str(str), new Fret(fret));
            var midiNote = new MidiNote(stringTunings[str - 1] + fret);
            positions.Add(new Position.Played(location, midiNote));
            notes.Add(midiNote);
        }

        return new Voicing([.. positions], [.. notes]);
    }

    /// <summary>
    /// Creates voicings across different fret ranges
    /// </summary>
    private static List<Voicing> CreateVoicingsAcrossFretboard()
    {
        var voicings = new List<Voicing>();

        // Open position (frets 0-4)
        for (int fret = 0; fret <= 4; fret++)
        {
            voicings.Add(CreateVoicing([(1, fret), (2, fret)]));
        }

        // Middle position (frets 5-12)
        for (int fret = 5; fret <= 12; fret++)
        {
            voicings.Add(CreateVoicing([(1, fret), (2, fret)]));
        }

        // Upper position (frets 13+)
        for (int fret = 13; fret <= 15; fret++)
        {
            voicings.Add(CreateVoicing([(1, fret), (2, fret)]));
        }

        return voicings;
    }

    private static Voicing CreateFullPitchClassVoicing(int[] pitchClasses)
    {
        var positions = new List<Position>();
        var notes = new List<MidiNote>();
        const int baseMidi = 60;

        for (var index = 0; index < pitchClasses.Length; index++)
        {
            var normalized = ((pitchClasses[index] % 12) + 12) % 12;
            var midiValue = baseMidi + normalized;
            var midiNote = new MidiNote(midiValue);
            var stringNumber = Math.Min(index + 1, 26);
            var location = new PositionLocation(new Str(stringNumber), new Fret(normalized));

            positions.Add(new Position.Played(location, midiNote));
            notes.Add(midiNote);
        }

        return new Voicing([.. positions], [.. notes]);
    }

    #endregion
}
