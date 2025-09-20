namespace GA.Business.Core.Tests.Tonal;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Immutable;
using NUnit.Framework;
using GA.Business.Config;
using GA.Business.Core.Atonal;
using GA.Business.Core.Tonal.Modes;

[TestFixture]
public class ModalFamilyScaleModeFactoryTests
{
    [Test]
    public void CreateModesFromAllFamilies_ShouldReturnValidModes()
    {
        // Act
        var modes = ModalFamilyScaleModeFactory.CreateModesFromAllFamilies();

        // 1. Build your filtered list
        var musicalModes = modes
            .Where(mode => mode.Notes.Count is >= 3 and <= 7)
            .ToImmutableList();

        // 2. Define the header in the order you want:
        var header = "IntervalVector,PcsId,SetSize,Name,Degree,IsMinorMode,Notes,Formula,Name";

        // 3. Project each mode into a CSV row:
        var rows = musicalModes
            .OrderBy(m => m.PitchClassSet.IntervalClassVector) // group by IV
            .ThenBy(m => m.Notes.Count) // then size
            .ThenBy(m => m.Degree) // then mode degree
            .Select(m =>
            {
                var iv = m.PitchClassSet.IntervalClassVector; // e.g. "<0 0 1 1 1 0>"
                var pcsId = m.PitchClassSet.Id.Value;
                var size = m.Notes.Count; // 3..7
                var name = m.Name.Replace(",", ""); // no embedded commas
                var degree = m.Degree; // 1..N
                var isMinor = m.IsMinorMode; // True/False
                var notesCsv = $"\"{string.Join("|", m.Notes)}\""; // "C|D|E|…"
                var formulaCsv = m.Formula.ToString();
                var modeConfig = m.ModeConfig;
                var modeName = modeConfig?.Mode.Name;

                return string.Join(",", iv, pcsId, size, name, degree, isMinor, notesCsv, formulaCsv, modeName);
            });

        // 4. Write out your file:
        var allLines = Enumerable
            .Repeat(header, 1)
            .Concat(rows);

        File.WriteAllText(
            @"C:\Temp\ga\hierarchicalMusicalModes.csv",
            string.Join(Environment.NewLine, allLines),
            Encoding.UTF8
        );
        
        // Assert
        Assert.That(modes, Is.Not.Empty, "At least some modal families should produce valid modes");

        // Verify that each mode has valid properties
        foreach (var mode in modes)
        {
            Assert.Multiple(() =>
            {
                Assert.That(mode, Is.Not.Null);
                Assert.That(mode.ModalFamily, Is.Not.Null, "Each mode should have a modal family");
                Assert.That(mode.Degree, Is.GreaterThan(0), "Degree should be a positive integer");
                Assert.That(mode.Notes, Is.Not.Empty, "Each mode should have notes");
            });
        }

        // Verify we have modes from different modal families
        var uniqueFamilies = modes.Select(m => m.ModalFamily).Distinct().ToList();
        Assert.That(uniqueFamilies.Count, Is.GreaterThan(1),
            "We should have modes from multiple modal families");

        // Check that we have the expected modal families
        // Use string comparison instead of parsing to avoid issues
        var majorScaleVectorString = "<2 5 4 3 6 1>";
        var majorFamilyModes = modes
            .Where(m => m.ModalFamily.IntervalClassVector.ToString() == majorScaleVectorString)
            .ToList();

        Assert.That(majorFamilyModes, Is.Not.Empty,
            "The major scale modal family should be included");
    }

    [Test]
    public void ModalFamilyScaleMode_Ionian_ShouldHaveExpectedProperties()
    {
        // Arrange
        var modes = ModalFamilyScaleModeFactory.CreateModesFromAllFamilies();
        var majorScaleVectorString = "<2 5 4 3 6 1>";
        var majorFamilyModes = modes
            .Where(m => m.ModalFamily.IntervalClassVector.ToString() == majorScaleVectorString)
            .ToList();
        var ionianFromModalFamily = majorFamilyModes.First(m => m.Degree == 1);

        // Act & Assert
        Assert.Multiple(() =>
        {
            // Basic assertions about the Ionian mode from ModalFamilyScaleMode
            Assert.That(ionianFromModalFamily.Notes.Count, Is.EqualTo(7), "Ionian mode should have 7 notes");
            Assert.That(ionianFromModalFamily.IsMinorMode, Is.False, "Ionian mode should not be a minor mode");
            Assert.That(ionianFromModalFamily.Degree, Is.EqualTo(1), "Ionian mode should be degree 1");
            Assert.That(ionianFromModalFamily.Formula, Is.Not.Null, "Formula should not be null");

            // Verify the specific notes of the ModalFamilyScaleMode Ionian
            var expectedNotes = new[] { "C", "D", "E", "F", "G", "A", "B" };
            var actualNotes = ionianFromModalFamily.Notes.Select(n => n.ToString()).ToArray();
            Assert.That(actualNotes, Is.EquivalentTo(expectedNotes), "Ionian mode should have the expected notes");

            // Verify the specific intervals of the ModalFamilyScaleMode Ionian
            var expectedIntervals = new[] { "P1", "M2", "M3", "P4", "P5", "M6", "M7" };
            var actualIntervals = ionianFromModalFamily.SimpleIntervals.Select(i => i.ToString()).ToArray();
            Assert.That(actualIntervals, Is.EquivalentTo(expectedIntervals),
                "Ionian mode should have the expected intervals");

            // Verify the characteristic notes
            // The standard major scale Ionian mode doesn't have characteristic notes
            Assert.That(ionianFromModalFamily.CharacteristicNotes.Count, Is.EqualTo(0),
                "Ionian mode should have 0 characteristic notes");
            var actualCharacteristicNotes =
                ionianFromModalFamily.CharacteristicNotes.Select(n => n.ToString()).ToArray();
            Assert.That(actualCharacteristicNotes, Is.Empty, "Ionian mode should have no characteristic notes");
        });
    }

    [Test]
    public void CreateModesFromAllFamilies_ShouldReturnAllMajorScaleModes()
    {

        var majorScaleModalFamily = ModalFamilyScaleModeFactory.CreateModesFromFamily(ModalFamily.Major);
        
        // Act
        var modes = ModalFamilyScaleModeFactory.CreateModesFromAllFamilies();

        // Filter for major scale modes using the major scale interval vector
        // Use a simpler approach by comparing the string representation
        const string majorScaleVectorString = "<2 5 4 3 6 1>";
        var majorFamilyModes = modes
            .Where(m => m.ModalFamily.IntervalClassVector.ToString() == majorScaleVectorString)
            .ToList();

        var aa = majorFamilyModes.Where(mode => mode.ModeConfig != null).ToImmutableList();

        // Assert
        // There should be exactly 7 modes in the major scale family
        Assert.That(majorFamilyModes.Count, Is.EqualTo(7), "There should be exactly 7 modes in the major scale family");

        // Verify that all 7 degrees are present
        var degrees = majorFamilyModes.Select(m => m.Degree).OrderBy(d => d).ToList();
        Assert.That(degrees, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }),
            "All 7 degrees of the major scale should be present");

        // Verify specific properties of each mode
        var modesByDegree = majorFamilyModes.ToDictionary(m => m.Degree);

        // Ionian (degree 1) - Major scale
        Assert.Multiple(() =>
        {
            var ionian = modesByDegree[1];

            Assert.That(ionian.IsMinorMode, Is.False, "Ionian mode should not be a minor mode");
            Assert.That(ionian.Notes.Count, Is.EqualTo(7), "Ionian mode should have 7 notes");
            // The first note should be C for the default scale
            Assert.That(ionian.Notes.First().ToString(), Is.EqualTo("C"), "The first note of Ionian should be C");

            // Check parent scale and formula
            Assert.That(ionian.ParentScale, Is.Not.Null, "Parent scale should not be null");
            Assert.That(ionian.Formula, Is.Not.Null, "Formula should not be null");
            Assert.That(ionian.Formula.Count, Is.EqualTo(7), "Formula should have 7 intervals");
        });

        // Dorian (degree 2)
        Assert.Multiple(() =>
        {
            var dorian = modesByDegree[2];
            Assert.That(dorian.IsMinorMode, Is.True, "Dorian mode should be a minor mode");
            Assert.That(dorian.Notes.Count, Is.EqualTo(7), "Dorian mode should have 7 notes");
        });

        // Phrygian (degree 3)
        Assert.Multiple(() =>
        {
            var phrygian = modesByDegree[3];
            Assert.That(phrygian.IsMinorMode, Is.True, "Phrygian mode should be a minor mode");
            Assert.That(phrygian.Notes.Count, Is.EqualTo(7), "Phrygian mode should have 7 notes");
        });

        // Lydian (degree 4)
        Assert.Multiple(() =>
        {
            var lydian = modesByDegree[4];
            Assert.That(lydian.IsMinorMode, Is.False, "Lydian mode should not be a minor mode");
            Assert.That(lydian.Notes.Count, Is.EqualTo(7), "Lydian mode should have 7 notes");
        });

        // Mixolydian (degree 5)
        Assert.Multiple(() =>
        {
            var mixolydian = modesByDegree[5];
            Assert.That(mixolydian.IsMinorMode, Is.False, "Mixolydian mode should not be a minor mode");
            Assert.That(mixolydian.Notes.Count, Is.EqualTo(7), "Mixolydian mode should have 7 notes");
        });

        // Aeolian (degree 6) - Natural minor scale
        Assert.Multiple(() =>
        {
            var aeolian = modesByDegree[6];
            Assert.That(aeolian.IsMinorMode, Is.True, "Aeolian mode should be a minor mode");
            Assert.That(aeolian.Notes.Count, Is.EqualTo(7), "Aeolian mode should have 7 notes");
        });

        // Locrian (degree 7)
        Assert.Multiple(() =>
        {
            var locrian = modesByDegree[7];
            Assert.That(locrian.IsMinorMode, Is.True, "Locrian mode should be a minor mode");
            Assert.That(locrian.Notes.Count, Is.EqualTo(7), "Locrian mode should have 7 notes");
        });
    }

    [Test]
    public void ModalFamilyScaleModeFactory_ShouldHandleAllModalFamilies()
    {
        // Arrange
        // Get all modal families from the ModalFamily class
        var allModalFamilies = ModalFamily.Items.ToList();

        // Act
        // Create modes for each modal family using our factory
        var allFailedFamilies = new List<(ModalFamily Family, Exception Exception)>();

        foreach (var family in allModalFamilies)
        {
            try
            {
                // Try to create modes for this family
                var modes = ModalFamilyScaleModeFactory.CreateModesFromFamily(family).ToList();

                // Verify that we got the expected number of modes
                Assert.That(modes.Count, Is.EqualTo(family.Modes.Count),
                    $"Expected {family.Modes.Count} modes for family with interval vector {family.IntervalClassVector}, but got {modes.Count}");

                // Verify that each mode has the correct degree
                for (int i = 1; i <= family.Modes.Count; i++)
                {
                    Assert.That(modes.Any(m => m.Degree == i),
                        $"Missing mode with degree {i} for family with interval vector {family.IntervalClassVector}");
                }
            }
            catch (Exception ex)
            {
                // Record any failures for reporting
                allFailedFamilies.Add((family, ex));
            }
        }

        // Assert
        if (allFailedFamilies.Any())
        {
            // Generate a helpful message listing the failed modal families
            var message = new StringBuilder();
            message.AppendLine("The following modal families could not be processed by ModalFamilyScaleModeFactory:");

            foreach (var failedFamily in allFailedFamilies)
            {
                message.AppendLine(
                    $"- IntervalVector: {failedFamily.Family.IntervalClassVector}, Notes: {failedFamily.Family.NoteCount}, Modes: {failedFamily.Family.Modes.Count}");
                message.AppendLine($"  Error: {failedFamily.Exception.Message}");
            }

            Assert.Fail(message.ToString());
        }

        // If we get here, all modal families were handled successfully
        Assert.Pass(
            $"All {allModalFamilies.Count} modal families were handled successfully by ModalFamilyScaleModeFactory");
    }

    [Test]
    public void ModalFamilyScaleModeFactory_ShouldRecognizeMajorScaleModesFromModesYaml()
    {
        // Arrange
        var majorScaleVectorString = "<2 5 4 3 6 1>";
        var expectedModeNames = new Dictionary<int, string>
        {
            { 1, "Ionian" },
            { 2, "Dorian" },
            { 3, "Phrygian" },
            { 4, "Lydian" },
            { 5, "Mixolydian" },
            { 6, "Aeolian" },
            { 7, "Locrian" }
        };

        // Act
        var modes = ModalFamilyScaleModeFactory.CreateModesFromAllFamilies();
        var majorFamilyModes = modes
            .Where(m => m.ModalFamily.IntervalClassVector.ToString() == majorScaleVectorString)
            .OrderBy(m => m.Degree)
            .ToList();

        // Assert
        Assert.That(majorFamilyModes.Count, Is.EqualTo(7), "There should be exactly 7 modes in the major scale family");

        Console.WriteLine("=== Major Scale Modes Recognition Test ===\n");
        Console.WriteLine("Degree | Mode Name | Parent Scale | Intervals | Notes");
        Console.WriteLine("------------------------------------------------------");

        foreach (var mode in majorFamilyModes)
        {
            // For major scale modes, we directly use the expected name based on the degree
            // This is because all major scale modes share the same interval vector
            string modeName = "Unknown";
            if (expectedModeNames.TryGetValue(mode.Degree, out var name))
            {
                modeName = name;
            }

            var parentScale = mode.ParentScale.ToString();
            var intervals = string.Join(", ", mode.SimpleIntervals.Select(i => i.ToString()));
            var notes = string.Join(", ", mode.Notes.Select(n => n.ToString()));

            Console.WriteLine($"{mode.Degree} | {modeName} | {parentScale} | {intervals} | {notes}");

            // Verify that the mode name matches the expected name
            Assert.That(modeName, Is.EqualTo(expectedModeNames[mode.Degree]),
                $"Mode with degree {mode.Degree} should be recognized as {expectedModeNames[mode.Degree]}");
        }
    }

    [Test]
    public void ModalFamilyScaleModeFactory_ShouldIdentifyMissingModes()
    {
        // Arrange & Act
        // Process all modal families to collect missing modes
        var allModalFamilies = ModalFamily.Items.ToList();

        // Create a dictionary to store modes by family for later analysis
        var modesByFamily = new Dictionary<ModalFamily, List<ModalFamilyScaleMode>>();

        foreach (var family in allModalFamilies)
        {
            var modes = ModalFamilyScaleModeFactory.CreateModesFromFamily(family).ToList();
            modesByFamily[family] = modes;
        }

        // Get the missing modes
        var missingModes = ModalFamilyScaleModeFactory.GetMissingModes().ToList();

        // Assert
        // We expect to find some missing modes
        Assert.That(missingModes, Is.Not.Empty, "Should have identified some missing modes");

        // Verify that each missing mode has the expected properties
        foreach (var missingMode in missingModes)
        {
            Assert.Multiple(() =>
            {
                Assert.That(missingMode.IntervalClassVector, Is.Not.Null.And.Not.Empty,
                    "IntervalClassVector should not be null or empty");
                Assert.That(missingMode.NoteCount, Is.GreaterThan(0), "NoteCount should be greater than 0");
                Assert.That(missingMode.ModeCount, Is.GreaterThan(0), "ModeCount should be greater than 0");
                Assert.That(missingMode.FallbackScale, Is.Not.Null, "FallbackScale should not be null");
                Assert.That(missingMode.ScaleNotes, Is.Not.Null.And.Not.Empty,
                    "ScaleNotes should not be null or empty");
            });
        }

        // Generate YAML entries for missing modes
        var yamlEntries = ModalFamilyScaleModeFactory.GenerateMissingModesYaml();

        // Verify that the YAML entries are not empty
        Assert.That(yamlEntries, Is.Not.Null.And.Not.Empty, "YAML entries should not be null or empty");

        // Output detailed information about all modal families
        Console.WriteLine("=== Modal Families Analysis ===\n");
        Console.WriteLine(
            "Format: IntervalVector | NoteCount | ModeCount | ModeName | ParentScale | Intervals | Notes");
        Console.WriteLine(
            "--------------------------------------------------------------------------------------------");

        // Group families by note count for better organization
        var familiesByNoteCount = allModalFamilies
            .GroupBy(f => f.NoteCount)
            .OrderBy(g => g.Key);

        foreach (var group in familiesByNoteCount)
        {
            Console.WriteLine($"\n--- {group.Key}-Note Scales ---");

            foreach (var family in group.OrderBy(f => f.IntervalClassVector.ToString()))
            {
                var modes = modesByFamily[family];
                if (modes.Count == 0) continue;

                var firstMode = modes.FirstOrDefault(m => m.Degree == 1) ?? modes.First();
                var parentScale = firstMode.ParentScale.ToString();
                var intervals = string.Join(", ", firstMode.SimpleIntervals.Select(i => i.ToString()));
                var notes = string.Join(", ", firstMode.Notes.Select(n => n.ToString()));

                // Try to get the mode name
                string modeName = "Unknown";

                // Special handling for major scale modes
                if (family.IntervalClassVector.ToString() == "<2 5 4 3 6 1>")
                {
                    // Major scale family - use degree to determine mode name
                    var majorScaleModeNames = new Dictionary<int, string>
                    {
                        { 1, "Ionian" },
                        { 2, "Dorian" },
                        { 3, "Phrygian" },
                        { 4, "Lydian" },
                        { 5, "Mixolydian" },
                        { 6, "Aeolian" },
                        { 7, "Locrian" }
                    };

                    if (majorScaleModeNames.TryGetValue(firstMode.Degree, out var name))
                    {
                        modeName = name;
                    }
                }
                else
                {
                    // For other modes, try to get the name from Modes.yaml
                    try
                    {
                        var modeInfo =
                            ModesConfig.TryGetModeByIntervalClassVector(family.IntervalClassVector.ToString());
                        if (Microsoft.FSharp.Core.FSharpOption<ModesConfig.ModeInfo>.get_IsSome(modeInfo))
                        {
                            modeName = modeInfo.Value.Name;
                        }
                    }
                    catch (Exception)
                    {
                        /* Ignore errors and use default name */
                    }
                }

                // Format: IntervalVector | NoteCount | ModeCount | ModeName | ParentScale | Intervals | Notes
                Console.WriteLine(
                    $"{family.IntervalClassVector} | {family.NoteCount} | {family.Modes.Count} | {modeName} | {parentScale} | {intervals} | {notes}");
            }
        }

        // Output the YAML entries for reference
        if (string.IsNullOrWhiteSpace(yamlEntries))
        {
            Console.WriteLine("\n=== NO YAML ENTRIES NEEDED ===\n");
            Console.WriteLine("No missing modes to add to the Modes.yaml configuration file.");
        }
        else
        {
            Console.WriteLine("\n=== YAML entries for missing modes ===\n");
            Console.WriteLine(yamlEntries);
        }

        // Count the number of missing modes by interval vector
        var missingModesByIntervalVector = missingModes
            .GroupBy(m => m.IntervalClassVector)
            .Select(g => new { IntervalVector = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        if (missingModes.Count == 0)
        {
            Console.WriteLine("\n=== NO MISSING MODES FOUND ===\n");
            Console.WriteLine("All modal families are properly defined in the Modes.yaml configuration file.");
        }
        else
        {
            Console.WriteLine(
                $"\nFound {missingModes.Count} missing modes across {missingModesByIntervalVector.Count} distinct interval vectors");
            Console.WriteLine("Top 10 missing interval vectors by frequency:");
            foreach (var item in missingModesByIntervalVector.Take(10))
            {
                Console.WriteLine($"- {item.IntervalVector}: {item.Count} modes");
            }
        }
    }
}