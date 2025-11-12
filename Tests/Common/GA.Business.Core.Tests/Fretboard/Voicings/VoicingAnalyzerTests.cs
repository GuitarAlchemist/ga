namespace GA.Business.Core.Tests.Fretboard.Voicings;

using Core.Fretboard.Voicings.Analysis;
using Core.Fretboard.Voicings.Core;
using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings;
using GA.Business.Core.Notes.Primitives;

/// <summary>
/// Unit tests for VoicingAnalyzer - comprehensive musical analysis of guitar voicings
/// </summary>
[TestFixture]
public class VoicingAnalyzerTests
{
    #region Mode Detection Tests - Major Scale Family

    [Test]
    public void DetectMode_IonianMode_ShouldIdentifyCorrectly()
    {
        // Arrange: C Major scale (Ionian) - C D E F G A B
        var pitchClasses = new[] { 0, 2, 4, 5, 7, 9, 11 };
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Ionian scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Ionian").Or.Contains("Major"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Major Scale Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(1));
        Assert.That(analysis.ModeInfo.NoteCount, Is.EqualTo(7));
    }

    [Test]
    public void DetectMode_DorianMode_ShouldIdentifyCorrectly()
    {
        // Arrange: D Dorian - D E F G A B C (2nd mode of C Major)
        var pitchClasses = new[] { 0, 2, 3, 5, 7, 9, 10 }; // C D Eb F G A Bb
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Dorian scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Dorian"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Major Scale Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(2));
        Assert.That(analysis.ModeInfo.NoteCount, Is.EqualTo(7));
    }

    [Test]
    public void DetectMode_PhrygianMode_ShouldIdentifyCorrectly()
    {
        // Arrange: E Phrygian - E F G A B C D (3rd mode of C Major)
        var pitchClasses = new[] { 0, 1, 3, 5, 7, 8, 10 }; // C Db Eb F G Ab Bb
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Phrygian scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Phrygian"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Major Scale Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(3));
    }

    [Test]
    public void DetectMode_LydianMode_ShouldIdentifyCorrectly()
    {
        // Arrange: F Lydian - F G A B C D E (4th mode of C Major)
        var pitchClasses = new[] { 0, 2, 4, 6, 7, 9, 11 }; // C D E F# G A B
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Lydian scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Lydian"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Major Scale Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(4));
    }

    [Test]
    public void DetectMode_MixolydianMode_ShouldIdentifyCorrectly()
    {
        // Arrange: G Mixolydian - G A B C D E F (5th mode of C Major)
        var pitchClasses = new[] { 0, 2, 4, 5, 7, 9, 10 }; // C D E F G A Bb
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Mixolydian scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Mixolydian"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Major Scale Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(5));
    }

    [Test]
    public void DetectMode_AeolianMode_ShouldIdentifyCorrectly()
    {
        // Arrange: A Aeolian (Natural Minor) - A B C D E F G (6th mode of C Major)
        var pitchClasses = new[] { 0, 2, 3, 5, 7, 8, 10 }; // C D Eb F G Ab Bb
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Aeolian scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Aeolian").Or.Contains("Minor"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Major Scale Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(6));
    }

    [Test]
    public void DetectMode_LocrianMode_ShouldIdentifyCorrectly()
    {
        // Arrange: B Locrian - B C D E F G A (7th mode of C Major)
        var pitchClasses = new[] { 0, 1, 3, 5, 6, 8, 10 }; // C Db Eb F Gb Ab Bb
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Locrian scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Locrian"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Major Scale Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(7));
    }

    #endregion

    #region Mode Detection Tests - Harmonic Minor Family

    [Test]
    public void DetectMode_HarmonicMinor_ShouldIdentifyCorrectly()
    {
        // Arrange: C Harmonic Minor - C D Eb F G Ab B
        var pitchClasses = new[] { 0, 2, 3, 5, 7, 8, 11 };
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Harmonic Minor scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Harmonic Minor"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Harmonic Minor Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(1));
        Assert.That(analysis.ModeInfo.NoteCount, Is.EqualTo(7));
    }

    [Test]
    public void DetectMode_PhrygianDominant_ShouldIdentifyCorrectly()
    {
        // Arrange: C Phrygian Dominant - C Db E F G Ab Bb (5th mode of Harmonic Minor)
        var pitchClasses = new[] { 0, 1, 4, 5, 7, 8, 10 };
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Phrygian Dominant scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Phrygian Dominant"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Harmonic Minor Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(5));
        Assert.That(analysis.ModeInfo.NoteCount, Is.EqualTo(7));
    }

    [Test]
    public void DetectMode_LocrianNatural6_ShouldIdentifyCorrectly()
    {
        // Arrange: C Locrian ♮6 - C Db Eb F Gb A Bb (2nd mode of Harmonic Minor)
        var pitchClasses = new[] { 0, 1, 3, 5, 6, 9, 10 };
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Locrian ♮6 scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Locrian"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Harmonic Minor Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(2));
    }

    #endregion

    #region Mode Detection Tests - Melodic Minor Family

    [Test]
    public void DetectMode_MelodicMinor_ShouldIdentifyCorrectly()
    {
        // Arrange: C Melodic Minor (Jazz Minor) - C D Eb F G A B
        var pitchClasses = new[] { 0, 2, 3, 5, 7, 9, 11 };
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Melodic Minor scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Melodic Minor").Or.Contains("Jazz Minor"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Melodic Minor Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(1));
        Assert.That(analysis.ModeInfo.NoteCount, Is.EqualTo(7));
    }

    [Test]
    public void DetectMode_Altered_ShouldIdentifyCorrectly()
    {
        // Arrange: C Altered (Super Locrian) - C Db Eb Fb Gb Ab Bb (7th mode of Melodic Minor)
        var pitchClasses = new[] { 0, 1, 3, 4, 6, 8, 10 };
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Altered scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Altered").Or.Contains("Super Locrian"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Melodic Minor Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(7));
    }

    [Test]
    public void DetectMode_LydianDominant_ShouldIdentifyCorrectly()
    {
        // Arrange: C Lydian Dominant (Overtone) - C D E F# G A Bb (4th mode of Melodic Minor)
        var pitchClasses = new[] { 0, 2, 4, 6, 7, 9, 10 };
        var pitchClassSet = new PitchClassSet(pitchClasses.Select(pc => PitchClass.FromValue(pc)));

        // Act
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Not.Null, "Should detect mode for Lydian Dominant scale");
        Assert.That(analysis.ModeInfo!.ModeName, Does.Contain("Lydian Dominant").Or.Contains("Overtone"));
        Assert.That(analysis.ModeInfo.FamilyName, Is.EqualTo("Melodic Minor Family"));
        Assert.That(analysis.ModeInfo.DegreeInFamily, Is.EqualTo(4));
    }

    #endregion

    #region Edge Cases and Atonal Contexts

    [Test]
    public void DetectMode_Triad_ShouldReturnNull()
    {
        // Arrange: C Major triad - only 3 notes, not a full scale
        var pitchClasses = new[] { 0, 4, 7 }; // C E G
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);

        // Act
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert
        Assert.That(analysis.ModeInfo, Is.Null, "Triads should not match modal families");
        Assert.That(analysis.PitchClassSet.Count, Is.EqualTo(3));
    }

    [Test]
    public void DetectMode_ChromaticVoicing_ShouldHandleGracefully()
    {
        // Arrange: Chromatic cluster - C C# D D# E
        var pitchClasses = new[] { 0, 1, 2, 3, 4 };
        var voicing = CreateVoicingFromPitchClasses(pitchClasses);

        // Act
        var analysis = VoicingAnalyzer.Analyze(voicing);

        // Assert - Should not crash, may or may not detect a mode
        Assert.That(analysis, Is.Not.Null);
        Assert.That(analysis.IntervallicInfo, Is.Not.Null);
        Assert.That(analysis.IntervallicInfo.Features, Does.Contain("Cluster (4 semitones)"));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a voicing from pitch classes for testing
    /// </summary>
    private static Voicing CreateVoicingFromPitchClasses(int[] pitchClasses)
    {
        // Create positions on a standard-tuned 6-string guitar
        var positions = new List<Position>();
        var notes = new List<MidiNote>();
        var stringTunings = new[] { 64, 59, 55, 50, 45, 40 }; // E A D G B E (MIDI note numbers for standard tuning)

        var pitchClassList = pitchClasses.ToList();
        var positionIndex = 0;

        for (int str = 0; str < 6 && positionIndex < pitchClasses.Length; str++)
        {
            if (positionIndex >= pitchClasses.Length) break;

            var targetPitchClass = pitchClasses[positionIndex];
            var stringTuning = stringTunings[str];

            // Find fret that produces the target pitch class
            var fret = (targetPitchClass - (stringTuning % 12) + 12) % 12;
            if (fret < 0) fret += 12;

            var location = new PositionLocation(new Str(str + 1), new Fret(fret));
            var midiNote = new MidiNote(stringTuning + fret);

            positions.Add(new Position.Played(location, midiNote));
            notes.Add(midiNote);
            positionIndex++;
        }

        return new Voicing(positions.ToArray(), notes.ToArray());
    }

    #endregion
}

