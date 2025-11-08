namespace GA.Business.Core.Tests.Tonal;

using System.IO;
using Core.Atonal;
using Core.Notes;
using Scales;

[TestFixture]
public class ModalFamilyTests
{
    private static void LogToFile(string message)
    {
        var logPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "modal_family_test_log.txt");
        File.AppendAllText(logPath, message + Environment.NewLine);
        TestContext.WriteLine(message);
    }

    [Test]
    public void MajorScaleModalFamily_BaseTest()
    {
        if (!ModalFamily.TryGetValue(IntervalClassVector.Parse("<2 5 4 3 6 1>"), out var modalFamily))
        {
            throw new Exception("Modal family not found");
        }

        foreach (var pitchClassSet in modalFamily.Modes)
        {
            ModesConfigCache.Instance.TryGetModeByPitchClassSetId(pitchClassSet.Id.Value, out var mode);
        }
    }

    [Test]
    public void MajorScaleModalFamily_ContainsAllMajorScaleModes()
    {
        // Clear previous log file
        var logPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "modal_family_test_log.txt");
        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }

        LogToFile("\n=== MAJOR SCALE MODAL FAMILY TEST ===\n");

        // Arrange
        // The major scale interval vector is <2 5 4 3 6 1>
        var majorScaleIntervalVectorString = "<2 5 4 3 6 1>";
        LogToFile($"Looking for modal family with interval vector: {majorScaleIntervalVectorString}");

        // Find the modal family with the major scale interval vector
        var majorScaleModalFamily = ModalFamily.Items
            .FirstOrDefault(family => family.IntervalClassVector.ToString() == majorScaleIntervalVectorString);

        // Assert that we found the major scale modal family
        Assert.That(majorScaleModalFamily, Is.Not.Null, "Major scale modal family should exist");
        LogToFile($"Found modal family with {majorScaleModalFamily!.Modes.Count} modes");
        LogToFile($"Modal family interval class vector: {majorScaleModalFamily.IntervalClassVector}");

        // Define the expected pitch class sets for all major scale modes
        var expectedModes = new Dictionary<string, string>
        {
            { "Ionian (Major)", "C D E F G A B" }, // 1st mode - C Ionian (C Major)
            { "Dorian", "D E F G A B C" }, // 2nd mode - D Dorian
            { "Phrygian", "E F G A B C D" }, // 3rd mode - E Phrygian
            { "Lydian", "F G A B C D E" }, // 4th mode - F Lydian
            { "Mixolydian", "G A B C D E F" }, // 5th mode - G Mixolydian
            { "Aeolian (Natural Minor)", "A B C D E F G" }, // 6th mode - A Aeolian (A Minor)
            { "Locrian", "B C D E F G A" } // 7th mode - B Locrian
        };

        LogToFile("\nExpected modes of the major scale:");
        foreach (var (modeName, notes) in expectedModes)
        {
            LogToFile($"- {modeName}: {notes}");
        }

        // Convert the expected mode notes to pitch class sets
        var expectedPitchClassSets = expectedModes
            .ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    AccidentedNoteCollection.TryParse(kvp.Value, null, out var notes);
                    var pitchClassSet = new PitchClassSet(notes.Select(n => n.PitchClass));
                    LogToFile($"  {kvp.Key} pitch class set: {pitchClassSet}");
                    return pitchClassSet;
                }
            );

        // Assert that the modal family has the expected number of modes
        Assert.That(majorScaleModalFamily.Modes.Count, Is.EqualTo(expectedPitchClassSets.Count),
            "Major scale modal family should have 7 modes");

        LogToFile("\nActual modes in the modal family:");
        var modeIndex = 1;
        foreach (var mode in majorScaleModalFamily.Modes)
        {
            LogToFile($"- Mode {modeIndex++}: {mode}");
        }

        // Assert that each expected pitch class set is in the modal family
        LogToFile("\nVerifying each expected mode is in the modal family:");
        foreach (var (modeName, expectedPitchClassSet) in expectedPitchClassSets)
        {
            var found = majorScaleModalFamily.Modes.Contains(expectedPitchClassSet);
            LogToFile($"- {modeName}: {(found ? "Found" : "Not found")} in modal family");
            Assert.That(majorScaleModalFamily.Modes, Contains.Item(expectedPitchClassSet),
                $"Major scale modal family should contain the {modeName} mode");
        }

        // Additional test: Create modes using the factory and verify they match our expectations
        var majorScale = Scale.Major; // C major scale by default
        LogToFile($"\nCreating modes from {majorScale} scale");

        // Create all modes of the major scale
        var modes = new List<ModalFamilyScaleMode>();
        for (var degree = 1; degree <= 7; degree++)
        {
            var mode = ModalFamilyScaleMode.FromScale(majorScale, degree);
            Assert.That(mode, Is.Not.Null, $"Failed to create mode with degree {degree}");
            modes.Add(mode);
            LogToFile($"- Created mode with degree {degree}: {mode.Name}");
            LogToFile($"  Notes: {string.Join(" ", mode.Notes)}");
            LogToFile($"  Intervals: {string.Join(" ", mode.SimpleIntervals)}");
            LogToFile($"  Pitch class set: {mode.PitchClassSet}");
        }

        // Verify that each mode has the correct pitch class set
        LogToFile("\nVerifying each created mode has the correct pitch class set:");
        foreach (var mode in modes)
        {
            var expectedPitchClassSet = expectedPitchClassSets.Values.ElementAt(mode.Degree - 1);
            var modeName = expectedModes.Keys.ElementAt(mode.Degree - 1);
            var matches = mode.PitchClassSet.Equals(expectedPitchClassSet);
            LogToFile(
                $"- Mode {mode.Degree} ({modeName}): {(matches ? "Matches" : "Does not match")} expected pitch class set");
            Assert.That(mode.PitchClassSet, Is.EqualTo(expectedPitchClassSet),
                $"Mode with degree {mode.Degree} should have the expected pitch class set");

            // Verify that the mode belongs to the major scale modal family
            var sameFamily = mode.ModalFamily.IntervalClassVector.Equals(majorScaleModalFamily.IntervalClassVector);
            LogToFile($"  Belongs to major scale modal family: {sameFamily}");
            Assert.That(mode.ModalFamily.IntervalClassVector, Is.EqualTo(majorScaleModalFamily.IntervalClassVector),
                $"Mode with degree {mode.Degree} should belong to the major scale modal family");
        }

        TestContext.WriteLine("\n=== TEST COMPLETED SUCCESSFULLY ===\n");
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
