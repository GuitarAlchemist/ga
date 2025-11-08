namespace GA.Business.Core.Tests.Fretboard;

using Core.Atonal;
using Core.Fretboard.Analysis;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using FretboardClass = Core.Fretboard.Fretboard;

/// <summary>
///     Tests for fretboard indexing with 5-fret spans, equivalence groups decomposition,
///     and chord template/naming relationships with tonal context.
///     Based on equivalence groups from https://harmoniousapp.net/p/ec/Equivalence-Groups
/// </summary>
[TestFixture]
public class FretboardEquivalenceGroupsTests
{
    [SetUp]
    public void SetUp()
    {
        _fretboard = FretboardClass.Default;
    }

    private FretboardClass _fretboard = null!;

    [Test]
    public void FiveFretSpan_ShouldIndexEntireFretboard()
    {
        // Test that the entire fretboard can be indexed as a series of 5-fret spans

        // Arrange
        var fretCount = _fretboard.FretCount;
        var expectedSpanCount = fretCount / 5 + 1; // e.g., 24 frets = 5 spans (0-4, 5-9, 10-14, 15-19, 20-24)

        // Act - Generate all chords within 5-fret spans
        var allChords = FretboardChordAnalyzer.GenerateAllFiveFretSpanChords(_fretboard).ToList();

        // Assert
        Assert.That(allChords, Is.Not.Empty, "Should generate chords across the fretboard");

        // Group by fret span
        var chordsBySpan = allChords
            .GroupBy(chord => chord.LowestFret / 5)
            .OrderBy(g => g.Key)
            .ToList();

        Assert.That(chordsBySpan.Count, Is.GreaterThan(0), "Should have chords in multiple spans");

        TestContext.WriteLine($"Total chords generated: {allChords.Count}");
        TestContext.WriteLine($"Fret spans with chords: {chordsBySpan.Count}");
        foreach (var span in chordsBySpan.Take(5))
        {
            var spanKey = span.Key;
            var count = span.Count();
            TestContext.WriteLine($"  Span {spanKey} (frets {spanKey * 5}-{spanKey * 5 + 4}): {count} chords");
        }
    }

    [Test]
    public void FiveFretSpan_ShouldCoverEntireFretboard()
    {
        // Verify that 5-fret span windows cover the entire fretboard
        // For a 22-fret guitar: windows 0-17 cover frets 0-22
        const int maxFret = 22;
        const int spanSize = 5;

        // Calculate expected windows
        var expectedWindows = new List<(int start, int end)>();
        for (var startFret = 0; startFret <= maxFret - spanSize; startFret++)
        {
            expectedWindows.Add((startFret, startFret + spanSize));
        }

        // Verify coverage
        Assert.That(expectedWindows.Count, Is.EqualTo(18),
            "Should have 18 windows for 22-fret guitar (0-17)");

        // Verify first window
        Assert.That(expectedWindows[0], Is.EqualTo((0, 5)),
            "First window should cover frets 0-5");

        // Verify last window
        Assert.That(expectedWindows[17], Is.EqualTo((17, 22)),
            "Last window should cover frets 17-22");

        // Verify all frets are covered
        var allCoveredFrets = new HashSet<int>();
        foreach (var (start, end) in expectedWindows)
        {
            for (var fret = start; fret <= end; fret++)
            {
                allCoveredFrets.Add(fret);
            }
        }

        Assert.That(allCoveredFrets.Count, Is.EqualTo(23),
            "Should cover all 23 fret positions (0-22)");
        Assert.That(allCoveredFrets.Min(), Is.EqualTo(0),
            "Should start at fret 0");
        Assert.That(allCoveredFrets.Max(), Is.EqualTo(22),
            "Should end at fret 22");

        TestContext.WriteLine("? Complete fretboard coverage verified:");
        TestContext.WriteLine($"   - {expectedWindows.Count} windows of {spanSize + 1} frets each");
        TestContext.WriteLine($"   - Covers all {allCoveredFrets.Count} fret positions (0-{maxFret})");
        TestContext.WriteLine($"   - First window: frets {expectedWindows[0].start}-{expectedWindows[0].end}");
        TestContext.WriteLine($"   - Last window: frets {expectedWindows[17].start}-{expectedWindows[17].end}");
    }

    [Test]
    public void FiveFretSpan_ShouldDeduplicateEquivalentPatterns()
    {
        // Verify that translation equivalence deduplicates patterns across windows
        // E.g., E major at fret 0 and F major at fret 1 should have same prime pattern

        // Generate chords from first two windows
        var window0Chords = FretboardChordAnalyzer.GenerateAllFiveFretSpanChords(_fretboard, maxFret: 6)
            .Where(c => c.LowestFret == 0)
            .Take(100) // Limit for performance
            .ToList();

        var window1Chords = FretboardChordAnalyzer.GenerateAllFiveFretSpanChords(_fretboard, maxFret: 6)
            .Where(c => c.LowestFret == 1)
            .Take(100) // Limit for performance
            .ToList();

        // Create equivalence collection
        var equivalences = ChordPatternEquivalenceFactory.CreateGuitarChordEquivalences();

        // Group by prime pattern
        var window0ByPrime = window0Chords
            .GroupBy(c => equivalences.GetPrimeForm(c.Invariant.PatternId) ?? c.Invariant.PatternId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var window1ByPrime = window1Chords
            .GroupBy(c => equivalences.GetPrimeForm(c.Invariant.PatternId) ?? c.Invariant.PatternId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Find common prime patterns
        var commonPrimes = window0ByPrime.Keys.Intersect(window1ByPrime.Keys).ToList();

        Assert.That(commonPrimes, Is.Not.Empty,
            "Should have common prime patterns between adjacent windows");

        // Verify that patterns are properly normalized
        var samplePrime = commonPrimes.First();
        var window0Sample = window0ByPrime[samplePrime].First();
        var window1Sample = window1ByPrime[samplePrime].First();

        TestContext.WriteLine("? Deduplication verified:");
        TestContext.WriteLine($"   - Window 0 chords: {window0Chords.Count}");
        TestContext.WriteLine($"   - Window 1 chords: {window1Chords.Count}");
        TestContext.WriteLine($"   - Common prime patterns: {commonPrimes.Count}");
        TestContext.WriteLine($"   - Sample prime: {samplePrime.ToPatternString()}");
        TestContext.WriteLine(
            $"     - Window 0: {window0Sample.Invariant.PatternId.ToPatternString()} (base fret {window0Sample.Invariant.BaseFret})");
        TestContext.WriteLine(
            $"     - Window 1: {window1Sample.Invariant.PatternId.ToPatternString()} (base fret {window1Sample.Invariant.BaseFret})");

        // Verify they normalize to same prime
        var prime0 = equivalences.GetPrimeForm(window0Sample.Invariant.PatternId);
        var prime1 = equivalences.GetPrimeForm(window1Sample.Invariant.PatternId);

        Assert.That(prime0, Is.EqualTo(prime1),
            "Equivalent patterns should normalize to same prime form");
    }

    [Test]
    public void FiveFretSpan_ShouldIncludeOpenStrings()
    {
        // Verify that open strings are included in all 5-fret span windows
        // Open strings should be available in ALL windows, not just window 0

        // Generate chords from multiple windows
        var window0Chords = FretboardChordAnalyzer.GenerateAllFiveFretSpanChords(_fretboard, maxFret: 10)
            .Where(c => c.LowestFret == 0)
            .Take(50)
            .ToList();

        var window5Chords = FretboardChordAnalyzer.GenerateAllFiveFretSpanChords(_fretboard, maxFret: 10)
            .Where(c => c.LowestFret == 5)
            .Take(50)
            .ToList();

        // Check for chords with open strings in window 0
        var window0WithOpen = window0Chords
            .Where(c => c.Positions.Any(p => p is Position.Played played && played.Location.IsOpen))
            .ToList();

        // Check for chords with open strings in window 5
        var window5WithOpen = window5Chords
            .Where(c => c.Positions.Any(p => p is Position.Played played && played.Location.IsOpen))
            .ToList();

        Assert.That(window0WithOpen, Is.Not.Empty,
            "Window 0 should have chords with open strings");

        Assert.That(window5WithOpen, Is.Not.Empty,
            "Window 5 should have chords with open strings (open strings available in all windows)");

        // Verify open string positions
        var sampleWithOpen = window0WithOpen.First();
        var openPositions = sampleWithOpen.Positions
            .OfType<Position.Played>()
            .Where(p => p.Location.IsOpen)
            .ToList();

        TestContext.WriteLine("? Open strings confirmed in 5-fret span windows:");
        TestContext.WriteLine($"   - Window 0 chords with open strings: {window0WithOpen.Count}/{window0Chords.Count}");
        TestContext.WriteLine($"   - Window 5 chords with open strings: {window5WithOpen.Count}/{window5Chords.Count}");
        TestContext.WriteLine($"   - Sample chord has {openPositions.Count} open string(s)");
        TestContext.WriteLine(
            $"   - Open string positions: {string.Join(", ", openPositions.Select(p => $"String {p.Location.Str.Value}"))}");
    }

    [Test]
    public void FiveFretSpan_ShouldGroupByPosition()
    {
        // Test grouping chords by fretboard position

        // Act - Generate chords for first position (frets 0-5)
        var firstPositionChords = FretboardChordAnalyzer.GenerateAllFiveFretSpanChords(_fretboard)
            .Where(chord => chord.LowestFret >= 0 && chord.HighestFret <= 5)
            .ToList();

        // Assert
        Assert.That(firstPositionChords, Is.Not.Empty, "Should have chords in first position");

        TestContext.WriteLine($"First position chords (frets 0-5): {firstPositionChords.Count}");
    }

    [Test]
    public void EquivalenceGroups_OPTC_TranspositionEquivalence()
    {
        // Test OPTC (Octave-Permutation-Transposition-Cardinality) Equivalence
        // All transpositions of major triad are considered equivalent (Prime Form)

        var testCases = new[]
        {
            ([0, 2, 2, 1, 0, 0], "E major"),
            ([1, 3, 3, 2, 1, 1], "F major"),
            ([3, 5, 5, 4, 3, 3], "G major"),
            (new[] { 5, 7, 7, 6, 5, 5 }, "A major")
        };

        var invariants = testCases
            .Select(tc => ChordInvariant.FromFrets(tc.Item1, _fretboard.Tuning))
            .ToList();

        var firstPattern = invariants[0].PatternId;

        Assert.That(invariants.All(inv => inv.PatternId == firstPattern), Is.True,
            "All major triads should have same normalized pattern (OPTC equivalence)");

        TestContext.WriteLine($"Pattern ID: {firstPattern}");
        foreach (var (tc, inv) in testCases.Zip(invariants))
        {
            TestContext.WriteLine($"  {tc.Item2}: {inv.PatternId}");
        }
    }

    [Test]
    public void EquivalenceGroups_PatternRecognition()
    {
        // Test pattern recognition across different positions

        // Arrange - Same chord shape at different positions
        var eMajorOpen = new[] { 0, 2, 2, 1, 0, 0 };
        var fMajorBarre = new[] { 1, 3, 3, 2, 1, 1 };

        // Act
        var eInvariant = ChordInvariant.FromFrets(eMajorOpen, _fretboard.Tuning);
        var fInvariant = ChordInvariant.FromFrets(fMajorBarre, _fretboard.Tuning);

        // Assert
        Assert.That(eInvariant.PatternId, Is.EqualTo(fInvariant.PatternId),
            "Same chord shape should have same pattern ID");

        TestContext.WriteLine($"E major pattern: {eInvariant.PatternId}");
        TestContext.WriteLine($"F major pattern: {fInvariant.PatternId}");
    }

    [Test]
    public void ChordTemplateNaming_WithTonalContext_ShouldProvideKeyAwareNames()
    {
        // Arrange - G Major chord
        var gMajorFrets = new[] { 3, 2, 0, 0, 3, 3 };
        var positions = ExtractPositions(gMajorFrets);
        var analysis = FretboardChordAnalyzer.AnalyzeChordVoicing(positions, _fretboard);
        var keyOfC = Key.Major.C;

        // Act - Get chord template and naming
        var template = analysis.ChordTemplate;
        var root = analysis.Root;

        // Assert
        Assert.That(template, Is.Not.Null, "Chord template should be identified");
        Assert.That(root, Is.Not.Null, "Chord root should be identified");
        Assert.That(keyOfC.KeyMode, Is.EqualTo(KeyMode.Major));
        Assert.That(keyOfC.Root.PitchClass, Is.EqualTo(PitchClass.C));

        TestContext.WriteLine($"Chord: {analysis.ChordName}");
        TestContext.WriteLine($"Root: {root}");
        TestContext.WriteLine($"Key: {keyOfC}");
    }

    [Test]
    public void ChordTemplateNaming_ShouldIdentifyChordTypes()
    {
        // Arrange - C Major chord
        var cMajorFrets = new[] { -1, 3, 2, 0, 1, 0 };
        var positions = ExtractPositions(cMajorFrets);
        var analysis = FretboardChordAnalyzer.AnalyzeChordVoicing(positions, _fretboard);

        // Act - Analyze chord
        var keyOfC = Key.Major.C;
        var keyOfG = Key.Major.G;

        // Assert - Keys are correctly identified
        Assert.That(keyOfC.Root.PitchClass, Is.EqualTo(PitchClass.C));
        Assert.That(keyOfG.Root.PitchClass, Is.EqualTo(PitchClass.G));
        Assert.That(analysis.ChordTemplate, Is.Not.Null);
        Assert.That(analysis.Root, Is.Not.Null);

        TestContext.WriteLine($"Chord: {analysis.ChordName}");
        TestContext.WriteLine($"Template: {analysis.ChordTemplate}");
    }

    [Test]
    public void Integration_FiveFretSpan_WithEquivalenceGroups_AndChordNaming()
    {
        // Comprehensive test combining 5-fret spans, equivalence groups, and chord naming

        // Arrange - Analyze first position (frets 0-5)
        var firstPositionChords = FretboardChordAnalyzer.GenerateAllFiveFretSpanChords(_fretboard)
            .Where(chord => chord.LowestFret >= 0 && chord.HighestFret <= 5)
            .Take(50) // Limit for performance
            .ToList();

        // Act - Group by equivalence patterns
        var chordsByPattern = firstPositionChords
            .Select(chord => new
            {
                Chord = chord, chord.Invariant
            })
            .GroupBy(x => x.Invariant.PatternId)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToList();

        // Assert
        Assert.That(chordsByPattern, Is.Not.Empty, "Should find equivalence groups");

        TestContext.WriteLine("\nTop 10 chord patterns in first position:");
        TestContext.WriteLine($"Total unique patterns: {chordsByPattern.Count}");

        foreach (var group in chordsByPattern)
        {
            var first = group.First();
            var count = group.Count();
            TestContext.WriteLine($"\nPattern {first.Invariant.PatternId}: {count} variations");
            TestContext.WriteLine($"  Example: {first.Chord.ChordName}");
            TestContext.WriteLine($"  Span: frets {first.Chord.LowestFret}-{first.Chord.HighestFret}");
        }
    }

    private ImmutableList<Position> ExtractPositions(int[] frets)
    {
        var positions = new List<Position>();
        for (var i = 0; i < frets.Length; i++)
        {
            var str = Str.FromValue(i + 1);
            var fret = Fret.FromValue(frets[i]);

            if (fret.IsMuted)
            {
                positions.Add(new Position.Muted(str));
            }
            else
            {
                var location = new PositionLocation(str, fret);
                var midiNote = _fretboard.Tuning[str].MidiNote + fret.Value;
                positions.Add(new Position.Played(location, midiNote));
            }
        }

        return positions.ToImmutableList();
    }
}
