namespace GA.Business.Core.Tests.Fretboard.Voicings;

using System.Diagnostics;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Fretboard.Voicings.Analysis;
using Core.Fretboard.Voicings.Core;
using Core.Notes.Primitives;

/// <summary>
/// Performance tests for VoicingAnalyzer and VoicingFilters
/// </summary>
[TestFixture]
public class VoicingAnalyzerPerformanceTests
{
    // Allow a small buffer for CI/hardware variance while keeping the bar strict.
    private const int _acceptableAnalysisTimeMs = 6; // 6ms per voicing
    private const int _acceptableFilteringTimeSec = 2; // 2 seconds for filtering 667K voicings

    #region Performance Tests - Analysis

    [Test]
    [Category("Performance")]
    public void Analyze_SingleVoicing_ShouldCompleteWithinThreshold()
    {
        // Arrange: Create a complex 7-note voicing
        var positions = new Position[]
        {
            new Position.Played(new PositionLocation(new Str(1), new Fret(0)), new MidiNote(64)),  // E
            new Position.Played(new PositionLocation(new Str(2), new Fret(0)), new MidiNote(59)),  // B
            new Position.Played(new PositionLocation(new Str(3), new Fret(0)), new MidiNote(55)),  // G
            new Position.Played(new PositionLocation(new Str(4), new Fret(2)), new MidiNote(52)),  // A
            new Position.Played(new PositionLocation(new Str(5), new Fret(2)), new MidiNote(47)),  // D
            new Position.Played(new PositionLocation(new Str(6), new Fret(0)), new MidiNote(40))   // E
        };
        var notes = new MidiNote[] { new(64), new(59), new(55), new(52), new(47), new(40) };
        var voicing = new Voicing(positions, notes);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var analysis = VoicingAnalyzer.Analyze(voicing);
        stopwatch.Stop();

        // Assert
        Assert.That(analysis, Is.Not.Null, "Analysis should complete successfully");
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThanOrEqualTo(_acceptableAnalysisTimeMs),
            $"Analysis should complete within {_acceptableAnalysisTimeMs}ms (actual: {stopwatch.ElapsedMilliseconds}ms)");

        Console.WriteLine($"Analysis time: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Test]
    [Category("Performance")]
    public void Analyze_100Voicings_ShouldMaintainPerformance()
    {
        // Arrange: Generate 100 different voicings
        var voicings = GenerateTestVoicings(100);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var analyses = voicings.Select(v => VoicingAnalyzer.Analyze(v)).ToList();
        stopwatch.Stop();

        var averageTimeMs = stopwatch.ElapsedMilliseconds / 100.0;

        // Assert
        Assert.That(analyses.Count, Is.EqualTo(100), "Should analyze all voicings");
        Assert.That(averageTimeMs, Is.LessThanOrEqualTo(_acceptableAnalysisTimeMs),
            $"Average analysis time should be within {_acceptableAnalysisTimeMs}ms (actual: {averageTimeMs:F2}ms)");

        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average time per voicing: {averageTimeMs:F2}ms");
    }

    #endregion

    #region Performance Tests - Batch Analysis

    [Test]
    [Category("Performance")]
    public void Analyze_1000Voicings_ShouldMaintainConsistentPerformance()
    {
        // Arrange: Generate 1000 different voicings
        var voicings = GenerateTestVoicings(1000);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var analyses = voicings.Select(v => VoicingAnalyzer.Analyze(v)).ToList();
        stopwatch.Stop();

        var averageTimeMs = stopwatch.ElapsedMilliseconds / 1000.0;
        var totalTimeSeconds = stopwatch.Elapsed.TotalSeconds;

        // Assert
        Assert.That(analyses.Count, Is.EqualTo(1000), "Should analyze all voicings");
        Assert.That(averageTimeMs, Is.LessThanOrEqualTo(_acceptableAnalysisTimeMs),
            $"Average analysis time should be within {_acceptableAnalysisTimeMs}ms (actual: {averageTimeMs:F2}ms)");
        Assert.That(totalTimeSeconds, Is.LessThan(10), "Total time should be under 10 seconds");

        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms ({totalTimeSeconds:F2}s)");
        Console.WriteLine($"Average time per voicing: {averageTimeMs:F2}ms");
        Console.WriteLine($"Throughput: {1000 / totalTimeSeconds:F0} voicings/second");
    }

    [Test]
    [Category("Performance")]
    public void Analyze_ComplexVoicings_ShouldHandleEfficiently()
    {
        // Arrange: Create complex 6-note voicings (full fretboard usage)
        var voicings = new List<Voicing>();
        var stringTunings = new[] { 64, 59, 55, 50, 45, 40 }; // E A D G B E
        for (int i = 0; i < 100; i++)
        {
            var positions = new Position[]
            {
                new Position.Played(new PositionLocation(new Str(1), new Fret(i % 13)), new MidiNote(stringTunings[0] + (i % 13))),
                new Position.Played(new PositionLocation(new Str(2), new Fret((i + 1) % 13)), new MidiNote(stringTunings[1] + ((i + 1) % 13))),
                new Position.Played(new PositionLocation(new Str(3), new Fret((i + 2) % 13)), new MidiNote(stringTunings[2] + ((i + 2) % 13))),
                new Position.Played(new PositionLocation(new Str(4), new Fret((i + 3) % 13)), new MidiNote(stringTunings[3] + ((i + 3) % 13))),
                new Position.Played(new PositionLocation(new Str(5), new Fret((i + 4) % 13)), new MidiNote(stringTunings[4] + ((i + 4) % 13))),
                new Position.Played(new PositionLocation(new Str(6), new Fret((i + 5) % 13)), new MidiNote(stringTunings[5] + ((i + 5) % 13)))
            };
            var notes = new MidiNote[]
            {
                new(stringTunings[0] + (i % 13)),
                new(stringTunings[1] + ((i + 1) % 13)),
                new(stringTunings[2] + ((i + 2) % 13)),
                new(stringTunings[3] + ((i + 3) % 13)),
                new(stringTunings[4] + ((i + 4) % 13)),
                new(stringTunings[5] + ((i + 5) % 13))
            };
            voicings.Add(new Voicing(positions, notes));
        }

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var analyses = voicings.Select(v => VoicingAnalyzer.Analyze(v)).ToList();
        stopwatch.Stop();

        var averageTimeMs = stopwatch.ElapsedMilliseconds / 100.0;

        // Assert
        Assert.That(analyses.Count, Is.EqualTo(100), "Should analyze all complex voicings");
        Assert.That(averageTimeMs, Is.LessThanOrEqualTo(_acceptableAnalysisTimeMs * 1.5),
            $"Complex voicings may take up to 1.5x longer (actual: {averageTimeMs:F2}ms)");

        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average time per complex voicing: {averageTimeMs:F2}ms");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates test voicings for performance testing
    /// </summary>
    private static List<Voicing> GenerateTestVoicings(int count)
    {
        var voicings = new List<Voicing>();
        var random = new Random(42); // Fixed seed for reproducibility

        var stringTunings = new[] { 64, 59, 55, 50, 45, 40 }; // E A D G B E
        for (int i = 0; i < count; i++)
        {
            var noteCount = random.Next(2, 7); // 2-6 notes
            var positions = new List<Position>();
            var notes = new List<MidiNote>();

            for (int j = 0; j < noteCount; j++)
            {
                var str = j + 1; // String 1-6
                var fret = random.Next(0, 13); // Fret 0-12
                var location = new PositionLocation(new Str(str), new Fret(fret));
                var midiNote = new MidiNote(stringTunings[str - 1] + fret);
                positions.Add(new Position.Played(location, midiNote));
                notes.Add(midiNote);
            }

            voicings.Add(new Voicing([.. positions], [.. notes]));
        }

        return voicings;
    }

    #endregion
}

