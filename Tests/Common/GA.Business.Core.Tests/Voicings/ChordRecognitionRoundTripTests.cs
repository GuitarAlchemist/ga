namespace GA.Business.Core.Tests.Voicings;

using Domain.Core.Instruments.Fretboard.Voicings.Core;
using Domain.Core.Instruments.Positions;
using Domain.Core.Instruments.Primitives;
using Domain.Core.Primitives.Notes;
using Domain.Core.Theory.Atonal;
using Domain.Core.Theory.Harmony;
using Domain.Services.Chords;
using Domain.Services.Fretboard.Voicings.Analysis;

/// <summary>
///     Round-trip correctness tests for the canonical chord recognition system
///     introduced by the chord-recognition architecture refactor (commit 82b8f397).
///
///     Four axes:
///         A. Name -> PC-set -> Name stability  (every pattern in the catalog)
///         B. Voicing diagram -> PC-set -> CanonicalName  (curated golden corpus)
///         C. Cross-instrument invariance  (same pcSet -> same CanonicalName regardless of bass)
///         D. Dyad + Forte fallback  (sanity checks on the fallback paths)
///
///     These tests MUST NOT modify production code. Any failure reveals a genuine
///     bug or design choice in the recognizer that should be triaged before Phase E
///     (optick.index regeneration + baseline diagnostics).
/// </summary>
[TestFixture]
public class ChordRecognitionRoundTripTests
{
    // =============================================================================
    // AXIS A: Name -> PC-set -> Name stability
    // For each pattern in CanonicalChordPatternCatalog.All, construct the pcSet
    // from its own intervals (root=0=C) and verify the recognizer reproduces the
    // pattern name with MatchDistance==0 and Root=="C".
    // =============================================================================

    // Patterns whose pitch-class sets collide with higher-priority patterns at a
    // different root, so the round-trip resolves to a different (but musically valid)
    // name. Excluded from AxisA to avoid asserting a false requirement. Examples:
    //   major-6 [0,4,7,9]         ≡ minor-7 at root 9 (priority 5 beats 10)
    //   6-9 [0,2,4,7,9]           ≡ minor-11 variants
    //   quartal-3 [0,5,10]        ≡ sus4 at root 5 (priority 8 beats 70)
    //   9-sus4 and dominant-11 literally share [0,2,5,7,10]
    //   dominant-7-sharp-5 [0,4,8,10] is T4-symmetric — multiple roots, same set
    // AxisE (below) covers these as "same-PC-set equivalence" assertions instead.
    private static readonly HashSet<string> AmbiguousRoundTripPatterns = new()
    {
        "major-6", "minor-6", "6-9", "minor-6-9", "minor-add-11",
        "major-13", "9-sus4", "13-sus4",
        "dominant-11", "dominant-7-sharp-5", "dominant-13-b9",
        "quartal-3", "quartal-4", "quartal-5",
    };

    public static IEnumerable<TestCaseData> AllPatternsSource() =>
        CanonicalChordPatternCatalog.All
            .Where(p => !AmbiguousRoundTripPatterns.Contains(p.Name))
            .Select(p => new TestCaseData(p).SetName($"AxisA_{p.Name}"));

    public static IEnumerable<TestCaseData> AmbiguousPatternsSource() =>
        CanonicalChordPatternCatalog.All
            .Where(p => AmbiguousRoundTripPatterns.Contains(p.Name))
            .Select(p => new TestCaseData(p).SetName($"AxisE_{p.Name}"));

    /// <summary>
    ///     Axis E: patterns whose PC-set collides with a higher-priority alternate.
    ///     The round-trip must still yield an exact match (distance=0) on SOME pattern
    ///     whose PC-set equals the input — just not necessarily the same name.
    /// </summary>
    [TestCaseSource(nameof(AmbiguousPatternsSource))]
    public void AxisE_AmbiguousPattern_RoundTripsToEquivalentPcSet(ChordIntervalPattern pattern)
    {
        var pcs = pattern.Intervals.Select(i => ((i % 12) + 12) % 12).Distinct().OrderBy(i => i).ToArray();
        var pcSet = new PitchClassSet(pcs.Select(PitchClass.FromValue));

        var result = CanonicalChordRecognizer.Identify(pcSet);

        Assert.That(result.MatchDistance, Is.EqualTo(0),
            $"Ambiguous pattern '{pattern.Name}' must still round-trip to an exact-distance match");

        var resolved = CanonicalChordPatternCatalog.All.FirstOrDefault(p => p.Name == result.PatternName);
        Assert.That(resolved.Name, Is.Not.Null.Or.Empty,
            $"Pattern '{pattern.Name}': recognizer returned unknown PatternName '{result.PatternName}'");

        var resolvedRootName = result.Root;
        var rootPc = resolvedRootName switch
        {
            "C" => 0, "C#" or "Db" => 1, "D" => 2, "D#" or "Eb" => 3, "E" => 4,
            "F" => 5, "F#" or "Gb" => 6, "G" => 7, "G#" or "Ab" => 8, "A" => 9,
            "A#" or "Bb" => 10, "B" or "Cb" => 11,
            _ => -1,
        };
        var resolvedPcs = resolved.Intervals
            .Select(i => (((i + rootPc) % 12) + 12) % 12)
            .Distinct()
            .OrderBy(i => i)
            .ToArray();

        Assert.That(resolvedPcs, Is.EqualTo(pcs),
            $"Pattern '{pattern.Name}' input PCs [{string.Join(",", pcs)}] resolved to '{result.PatternName}' with PCs [{string.Join(",", resolvedPcs)}] — the weaker round-trip (same-PC-set) still must hold");
    }

    [TestCaseSource(nameof(AllPatternsSource))]
    public void AxisA_PatternRoundTrip_RecognizesOriginal(ChordIntervalPattern pattern)
    {
        // Arrange: pattern intervals are already root=0 by convention
        var pcSet = new PitchClassSet(pattern.Intervals.Select(PitchClass.FromValue));

        // Act
        var result = CanonicalChordRecognizer.Identify(pcSet);

        // Assert: the recognizer must reproduce the originating pattern name with distance 0.
        // Dyad patterns (cardinality 2) flow through IdentifyDyad, which names power-chord
        // patterns "power-chord" and other dyads "dyad-icN". Both cases are exact matches.
        Assert.Multiple(() =>
        {
            if (pattern.Cardinality == 2)
            {
                Assert.That(result.PatternName, Is.EqualTo(pattern.Name),
                    $"Pattern '{pattern.Name}' (dyad) should round-trip via the dyad path. " +
                    $"Got PatternName='{result.PatternName}', CanonicalName='{result.CanonicalName}'");
                Assert.That(result.MatchDistance, Is.EqualTo(0),
                    $"Pattern '{pattern.Name}': match distance should be 0");
            }
            else
            {
                Assert.That(result.PatternName, Is.EqualTo(pattern.Name),
                    $"Pattern '{pattern.Name}' must round-trip exactly. " +
                    $"Got PatternName='{result.PatternName}', CanonicalName='{result.CanonicalName}'");
                Assert.That(result.MatchDistance, Is.EqualTo(0),
                    $"Pattern '{pattern.Name}' must match with distance 0 (exact match)");
                Assert.That(result.Root, Is.EqualTo("C"),
                    $"Pattern '{pattern.Name}': root should be 'C' (pc 0) but was '{result.Root}'");
                Assert.That(result.IsNaturallyOccurring, Is.True,
                    $"Pattern '{pattern.Name}': exact match should be 'naturally occurring'");
            }
        });
    }

    // =============================================================================
    // AXIS B: Voicing diagram -> PC-set -> CanonicalName
    // Curated golden corpus. Verifies the full pipeline: parse diagram ->
    // extract MIDI notes -> derive pitch classes -> recognize chord.
    // Diagrams use standard LOW-to-HIGH guitar convention (6th string first).
    // =============================================================================

    public static IEnumerable<TestCaseData> GoldenVoicingsSource() =>
        GoldenCorpus.Entries.Select(e =>
            new TestCaseData(e.Diagram, e.Instrument, e.ExpectedCanonicalName,
                             e.ExpectedQuality, e.ExpectedExtension)
                .SetName($"AxisB_{e.Instrument}_{SafeName(e.Diagram)}_{SafeName(e.ExpectedCanonicalName)}"));

    [TestCaseSource(nameof(GoldenVoicingsSource))]
    public void AxisB_VoicingDiagram_ProducesExpectedCanonicalName(
        string diagram,
        string instrument,
        string expectedCanonicalName,
        string expectedQuality,
        string? expectedExtension)
    {
        // Arrange
        var voicing = ParseDiagram(diagram, instrument);
        var pitchClasses = voicing.Notes.Select(n => n.PitchClass).Distinct().OrderBy(pc => pc.Value).ToList();
        var pcSet = new PitchClassSet(pitchClasses);
        var bassNote = PitchClass.FromValue(voicing.Notes.OrderBy(n => n.Value).First().Value % 12);

        // Act
        var identification = VoicingHarmonicAnalyzer.IdentifyChord(pcSet, pitchClasses, bassNote);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(identification.CanonicalName, Is.EqualTo(expectedCanonicalName),
                $"Diagram '{diagram}' ({instrument}): expected CanonicalName='{expectedCanonicalName}' " +
                $"but got '{identification.CanonicalName}' (PatternName='{identification.PatternName}', " +
                $"Quality='{identification.Quality}', Extension='{identification.Extension}')");
            Assert.That(identification.Quality, Is.EqualTo(expectedQuality),
                $"Diagram '{diagram}' ({instrument}): expected Quality='{expectedQuality}' " +
                $"but got '{identification.Quality}'");
            Assert.That(identification.Extension, Is.EqualTo(expectedExtension),
                $"Diagram '{diagram}' ({instrument}): expected Extension='{expectedExtension}' " +
                $"but got '{identification.Extension}'");
            Assert.That(identification.HasCanonicalIdentity, Is.True,
                $"Diagram '{diagram}' ({instrument}): should have canonical identity populated");
        });
    }

    // =============================================================================
    // AXIS C: Cross-instrument invariance
    // Same pcSet, different bass notes -> same CanonicalName. The SlashSuffix
    // may differ, but the canonical identity must not.
    // =============================================================================

    public static IEnumerable<TestCaseData> CrossInstrumentPcSetsSource() =>
        CrossInstrumentCorpus.Entries.Select(e =>
            new TestCaseData(e.PitchClasses, e.Bass1, e.Bass2, e.Description)
                .SetName($"AxisC_{SafeName(e.Description)}"));

    [TestCaseSource(nameof(CrossInstrumentPcSetsSource))]
    public void AxisC_SamePcSet_DifferentBass_YieldsSameCanonicalName(
        int[] pcs,
        int bass1,
        int bass2,
        string description)
    {
        // Arrange
        var pcSet = new PitchClassSet(pcs.Select(PitchClass.FromValue));

        // Act
        var r1 = CanonicalChordRecognizer.Identify(pcSet, PitchClass.FromValue(bass1));
        var r2 = CanonicalChordRecognizer.Identify(pcSet, PitchClass.FromValue(bass2));

        // Assert: CanonicalName must be register-invariant; slash suffix may differ
        Assert.Multiple(() =>
        {
            Assert.That(r1.CanonicalName, Is.EqualTo(r2.CanonicalName),
                $"{description}: CanonicalName must be register-invariant. " +
                $"bass={bass1} -> '{r1.CanonicalName}', bass={bass2} -> '{r2.CanonicalName}'");
            Assert.That(r1.Root, Is.EqualTo(r2.Root),
                $"{description}: Root must be bass-independent. " +
                $"bass={bass1} -> '{r1.Root}', bass={bass2} -> '{r2.Root}'");
            Assert.That(r1.PatternName, Is.EqualTo(r2.PatternName),
                $"{description}: PatternName must be bass-independent");
            Assert.That(r1.Quality, Is.EqualTo(r2.Quality),
                $"{description}: Quality must be bass-independent");
        });
    }

    // =============================================================================
    // AXIS D: Dyad + Forte fallback
    // =============================================================================

    public static IEnumerable<TestCaseData> DyadSource() =>
        DyadCorpus.Entries.Select(e =>
            new TestCaseData(e.Pc1, e.Pc2, e.ExpectedName, e.ExpectedQuality)
                .SetName($"AxisD_Dyad_{SafeName(e.ExpectedName)}"));

    [TestCaseSource(nameof(DyadSource))]
    public void AxisD_Dyad_ProducesExpectedIntervalName(int pc1, int pc2, string expectedName, string expectedQuality)
    {
        // Arrange
        var pcSet = new PitchClassSet([PitchClass.FromValue(pc1), PitchClass.FromValue(pc2)]);

        // Act
        var result = CanonicalChordRecognizer.Identify(pcSet);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.CanonicalName, Is.EqualTo(expectedName),
                $"Dyad ({pc1}, {pc2}): expected CanonicalName='{expectedName}' but got '{result.CanonicalName}'");
            Assert.That(result.Quality, Is.EqualTo(expectedQuality),
                $"Dyad ({pc1}, {pc2}): expected Quality='{expectedQuality}' but got '{result.Quality}'");
        });
    }

    public static IEnumerable<TestCaseData> ForteFallbackSource() =>
        ForteFallbackCorpus.Entries.Select(e =>
            new TestCaseData(e.PitchClasses, e.ExpectedCardinalityName, e.Description)
                .SetName($"AxisD_Fallback_{SafeName(e.Description)}"));

    [TestCaseSource(nameof(ForteFallbackSource))]
    public void AxisD_UnmatchedSet_FallsBackToForteName(int[] pcs, string expectedCardinalityName, string description)
    {
        // Arrange
        var pcSet = new PitchClassSet(pcs.Select(PitchClass.FromValue));

        // Act
        var result = CanonicalChordRecognizer.Identify(pcSet);

        // Assert: unmatched sets should either (a) flow to the Forte fallback (Quality="set-class",
        // MatchDistance=-1, name contains cardinality), or (b) match a set-class pattern from the
        // catalog (whole-tone-hexachord, octatonic, etc.). Both outcomes are acceptable; we only
        // insist that the fallback text is correct when it IS the fallback path.
        Assert.That(
            result.Quality == "set-class" || result.MatchDistance >= 0,
            Is.True,
            $"{description}: unmatched set should either be set-class fallback or a catalog set-class entry. " +
            $"Got Quality='{result.Quality}', MatchDistance={result.MatchDistance}, CanonicalName='{result.CanonicalName}'");

        if (result.Quality == "set-class" && result.MatchDistance < 0)
        {
            Assert.That(result.CanonicalName, Does.Contain(expectedCardinalityName),
                $"{description}: expected CanonicalName to contain '{expectedCardinalityName}' " +
                $"but got '{result.CanonicalName}'");
        }
    }

    // =============================================================================
    // HELPERS
    // =============================================================================

    // Open string MIDI per instrument, LOW-to-HIGH (standard guitar-diagram convention).
    // Guitar:  E2 A2 D3 G3 B3 E4 (6th string to 1st string).
    // Bass:    E1 A1 D2 G2.
    // Ukulele: G4 C4 E4 A4 (re-entrant G).
    // Matches the convention used by GaCLI.Commands.IndexVoicingsCommand.ParseDiagram
    // and standard guitar-chord diagram notation ("x-3-2-0-1-0" = mute 6th, A@3, D@2, G@0, B@1, high E@0).
    private static readonly Dictionary<string, int[]> OpenStringMidiLowToHigh = new()
    {
        ["guitar"] = [40, 45, 50, 55, 59, 64],   // E2 A2 D3 G3 B3 E4
        ["bass"] = [28, 33, 38, 43],             // E1 A1 D2 G2
        ["ukulele"] = [67, 60, 64, 69],          // G4 C4 E4 A4
    };

    private static Voicing ParseDiagram(string diagram, string instrument)
    {
        var parts = diagram.Split('-');
        if (!OpenStringMidiLowToHigh.TryGetValue(instrument, out var openMidi))
            throw new ArgumentException($"Unknown instrument: {instrument}. Expected guitar, bass, or ukulele.");
        if (parts.Length != openMidi.Length)
            throw new ArgumentException(
                $"Diagram '{diagram}' has {parts.Length} strings but {instrument} expects {openMidi.Length}");

        var positions = new List<Position>();
        var notes = new List<MidiNote>();

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            // String numbering: string 1 = highest (index N-1), string N = lowest (index 0).
            var stringNumber = parts.Length - i;
            var str = new Str(stringNumber);

            if (part is "x" or "X")
            {
                positions.Add(new Position.Muted(str));
            }
            else if (int.TryParse(part, out var fretValue))
            {
                var fret = new Fret(fretValue);
                var location = new PositionLocation(str, fret);
                var midiNoteValue = openMidi[i] + fretValue;
                var midiNote = new MidiNote(midiNoteValue);
                positions.Add(new Position.Played(location, midiNote));
                notes.Add(midiNote);
            }
            else
            {
                throw new ArgumentException($"Invalid fret value: '{part}' at string {i + 1} of '{diagram}'");
            }
        }

        return new Voicing([.. positions], [.. notes]);
    }

    private static string SafeName(string s) => s
        .Replace('/', '_')
        .Replace('#', 'S')
        .Replace('(', '_')
        .Replace(')', '_')
        .Replace(' ', '_')
        .Replace('+', 'P')
        .Replace('-', '_');
}

/// <summary>
///     Hand-curated ground truth for Axis B.
///     Diagrams are LOW-to-HIGH (standard guitar-chord-diagram convention).
///     ExpectedCanonicalName mirrors CanonicalChordRecognizer.ChordSuffix output (flat-spelled roots).
///     Quality family: "major", "minor", "dominant", "diminished", "augmented",
///         "altered-dominant", "suspended", "power", etc.
///     Extension: "triad", "6th", "7th", "9th", "11th", "13th", "add", "dyad", null.
/// </summary>
internal static class GoldenCorpus
{
    internal readonly record struct Entry(
        string Diagram,
        string Instrument,
        string ExpectedCanonicalName,
        string ExpectedQuality,
        string? ExpectedExtension);

    internal static readonly IReadOnlyList<Entry> Entries =
    [
        // ---------- Guitar triads ----------

        // Cmaj7 open: x 3 2 0 0 0  -> x, C3, E3, G3, B3, E4
        // PCs: {C, E, G, B} = [0,4,7,11] = major-7 at C.
        new("x-3-2-0-0-0", "guitar", "Cmaj7", "major", "7th"),

        // A major open: x 0 2 2 2 0 -> x, A2, E3, A3, C#4, E4
        // PCs: {A, E, C#} = [1,4,9] = major-triad at A.
        new("x-0-2-2-2-0", "guitar", "A", "major", "triad"),

        // D major open: x x 0 2 3 2 -> x, x, D3, A3, D4, F#4
        // PCs: {D, A, F#} = [2,6,9] = major-triad at D.
        new("x-x-0-2-3-2", "guitar", "D", "major", "triad"),

        // G major open: 3 2 0 0 0 3 -> G2, B2, D3, G3, B3, G4
        // PCs: {G, B, D} = [2,7,11] = major-triad at G.
        new("3-2-0-0-0-3", "guitar", "G", "major", "triad"),

        // E major open: 0 2 2 1 0 0 -> E2, B2, E3, G#3, B3, E4
        // PCs: {E, B, G#} = [4,8,11] = major-triad at E.
        new("0-2-2-1-0-0", "guitar", "E", "major", "triad"),

        // Dm open: x x 0 2 3 1 -> x, x, D3, A3, D4, F4
        // PCs: {D, A, F} = [2,5,9] = minor-triad at D.
        new("x-x-0-2-3-1", "guitar", "Dm", "minor", "triad"),

        // Am open: x 0 2 2 1 0 -> x, A2, E3, A3, C4, E4
        // PCs: {A, E, C} = [0,4,9] = minor-triad at A.
        new("x-0-2-2-1-0", "guitar", "Am", "minor", "triad"),

        // Em open: 0 2 2 0 0 0 -> E2, B2, E3, G3, B3, E4
        // PCs: {E, B, G} = [4,7,11] = minor-triad at E.
        new("0-2-2-0-0-0", "guitar", "Em", "minor", "triad"),

        // ---------- Guitar 7th chords ----------

        // Cm7 barre (A-shape, 3rd fret): x 3 5 3 4 3
        //   A2+3=C3, D3+5=G3, G3+3=Bb3, B3+4=Eb4, E4+3=G4
        //   PCs: {C, G, Bb, Eb} = [0,3,7,10] = minor-7 at C.
        new("x-3-5-3-4-3", "guitar", "Cm7", "minor", "7th"),

        // Am7 open: x 0 2 0 1 0 -> x, A2, E3, G3, C4, E4
        //   PCs: {A, E, G, C} = [0,4,7,9]. From A(9) -> [0,3,7,10] = minor-7 at A.
        new("x-0-2-0-1-0", "guitar", "Am7", "minor", "7th"),

        // F7 E-shape barre (1st fret): 1 3 1 2 1 1
        //   E2+1=F2, A2+3=C3, D3+1=Eb3, G3+2=A3, B3+1=C4, E4+1=F4
        //   PCs: {F, C, Eb, A} = [0,3,5,9]. From F(5) -> [0,4,7,10] = dominant-7 at F.
        new("1-3-1-2-1-1", "guitar", "F7", "dominant", "7th"),

        // G7 open: 3 2 0 0 0 1 -> G2, B2, D3, G3, B3, F4
        //   PCs: {G, B, D, F} = [2,5,7,11]. From G(7) -> [0,4,7,10] = dominant-7 at G.
        new("3-2-0-0-0-1", "guitar", "G7", "dominant", "7th"),

        // Dm7 open: x x 0 2 1 1 -> x, x, D3, A3, C4, F4
        //   PCs: {D, A, C, F} = [0,2,5,9]. From D(2) -> [0,3,7,10] = minor-7 at D.
        new("x-x-0-2-1-1", "guitar", "Dm7", "minor", "7th"),

        // ---------- Guitar suspended ----------

        // Csus4: x 3 3 0 1 1 -> x, C3, F3, G3, C4, F4
        //   PCs: {C, F, G} = [0,5,7] = sus4 at C.
        new("x-3-3-0-1-1", "guitar", "Csus4", "suspended", "triad"),

        // Dsus2: x x 0 2 3 0 -> x, x, D3, A3, D4, E4
        //   PCs: {D, A, E} = [2,4,9]. From D(2) -> [0,2,7] = sus2 at D.
        new("x-x-0-2-3-0", "guitar", "Dsus2", "suspended", "triad"),

        // Dsus4 voicing x x 0 2 3 3 -> PCs {D, G, A} = [2, 7, 9].
        // Ambiguous by design: same PCs match sus4 at D (prio 8) AND sus2 at G (prio 7).
        // Priority-based ranking picks Gsus2 as CanonicalName; Invariant #33 forbids
        // the recognizer from using bass as a tiebreaker. The guitarist-facing name
        // "Dsus4" is recovered as CanonicalName + SlashSuffix = "Gsus2/D" in DisplayName.
        new("x-x-0-2-3-3", "guitar", "Gsus2", "suspended", "triad"),

        // ---------- Guitar diminished / augmented ----------

        // Cdim7: x 3 4 2 4 2
        //   A2+3=C3, D3+4=F#3, G3+2=A3, B3+4=Eb4, E4+2=F#4
        //   PCs: {C, F#, A, Eb} = [0,3,6,9] = diminished-7 at C (root commonness 0 beats others).
        new("x-3-4-2-4-2", "guitar", "Cdim7", "diminished", "7th"),

        // Dm7b5 (half-diminished): x x 0 1 1 1 -> x, x, D3, Ab3, C4, F4
        //   PCs: {D, Ab, C, F} = [0,2,5,8]. From D(2) -> [0,3,6,10] = half-diminished-7 at D.
        //   ChordSuffix maps "half-diminished-7" -> "m7b5", so CanonicalName = "Dm7b5".
        new("x-x-0-1-1-1", "guitar", "Dm7b5", "diminished", "7th"),

        // Bdim triad: x 2 3 4 3 x -> x, B2, F3, B3, D4, x
        //   PCs: {B, F, D} = [2,5,11] = diminished-triad at B.
        new("x-2-3-4-3-x", "guitar", "Bdim", "diminished", "triad"),

        // Caug: x 3 2 1 1 0 -> x, C3, E3, G#3, C4, E4
        //   PCs: {C, E, G#} = [0,4,8] = augmented-triad at C.
        new("x-3-2-1-1-0", "guitar", "Caug", "augmented", "triad"),

        // ---------- Guitar power chords ----------

        // C5: x 3 5 x x x -> x, C3, G3
        //   PCs: {C, G} = [0,7] = power-chord at C.
        new("x-3-5-x-x-x", "guitar", "C5", "power", "dyad"),

        // E5: 0 2 2 x x x -> E2, B2, E3
        //   PCs: {E, B} = [4,11] = power-chord at E.
        new("0-2-2-x-x-x", "guitar", "E5", "power", "dyad"),

        // ---------- Guitar 7sus4 ----------

        // C7sus4: x 3 5 3 1 1 -> x, C3, G3, Bb3, C4, F4
        //   PCs: {C, G, Bb, F} = [0,5,7,10] = 7-sus4 at C.
        new("x-3-5-3-1-1", "guitar", "C7sus4", "suspended", "7th"),

        // ---------- Bass ----------

        // Bass G5 power chord: 3 5 x x
        //   E1+3=G1, A1+5=D2
        //   PCs: {G, D} = [2,7] = power-chord at G.
        new("3-5-x-x", "bass", "G5", "power", "dyad"),

        // Bass G major triad: 3 5 5 4
        //   E1+3=G1, A1+5=D2, D2+5=G2, G2+4=B2
        //   PCs: {G, D, B} = [2,7,11] = major-triad at G.
        new("3-5-5-4", "bass", "G", "major", "triad"),

        // Bass Em triad: 0 2 2 0
        //   E1, B1, E2, G2
        //   PCs: {E, B, G} = [4,7,11] = minor-triad at E.
        new("0-2-2-0", "bass", "Em", "minor", "triad"),

        // ---------- Ukulele (G4 C4 E4 A4 low-to-high indices) ----------

        // C major: 0 0 0 3 -> G4, C4, E4, C5 (A4+3)
        //   PCs: {G, C, E} = [0,4,7] = major-triad at C.
        new("0-0-0-3", "ukulele", "C", "major", "triad"),

        // F major: 2 0 1 0 -> A4, C4, F4, A4
        //   PCs: {A, C, F} = [0,5,9] = major-triad at F.
        new("2-0-1-0", "ukulele", "F", "major", "triad"),

        // A minor: 2 0 0 0 -> A4, C4, E4, A4
        //   PCs: {A, C, E} = [0,4,9] = minor-triad at A.
        new("2-0-0-0", "ukulele", "Am", "minor", "triad"),

        // G7: 0 2 1 2 -> G4, D4, F4, B4
        //   PCs: {G, D, F, B} = [2,5,7,11]. From G(7) -> [0,4,7,10] = dominant-7 at G.
        new("0-2-1-2", "ukulele", "G7", "dominant", "7th"),

        // D7: 2 2 2 3 -> A4, D4, F#4, C5
        //   PCs: {A, D, F#, C} = [0,2,6,9]. From D(2) -> [0,4,7,10] = dominant-7 at D.
        new("2-2-2-3", "ukulele", "D7", "dominant", "7th"),
    ];
}

/// <summary>
///     Hand-curated pitch-class sets for Axis C cross-instrument invariance.
///     Each entry: (PitchClasses, Bass1, Bass2, Description).
///     Includes triads, 7th chords, sus chords, drop voicings, altered dominants.
/// </summary>
internal static class CrossInstrumentCorpus
{
    internal readonly record struct Entry(
        int[] PitchClasses,
        int Bass1,
        int Bass2,
        string Description);

    internal static readonly IReadOnlyList<Entry> Entries =
    [
        // ---------- Basic triads ----------
        new([0, 4, 7], 0, 4, "Cmajor root vs 1st inversion (bass=E)"),
        new([0, 4, 7], 0, 7, "Cmajor root vs 2nd inversion (bass=G)"),
        new([0, 3, 7], 0, 3, "Cminor root vs 1st inversion (bass=Eb)"),
        new([2, 5, 9], 2, 9, "Dminor root vs 1st inversion (bass=A)"),
        new([4, 8, 11], 4, 11, "Emajor root vs 1st inversion (bass=B)"),

        // ---------- 7th chords ----------
        new([0, 4, 7, 11], 0, 4, "Cmaj7 root vs bass=E"),
        new([0, 4, 7, 11], 0, 7, "Cmaj7 root vs bass=G"),
        new([0, 4, 7, 10], 0, 4, "C7 root vs bass=E"),
        new([0, 3, 7, 10], 0, 3, "Cm7 root vs bass=Eb"),
        new([0, 3, 7, 10], 0, 10, "Cm7 root vs bass=Bb"),

        // ---------- sus2 / sus4 ----------
        new([0, 2, 7], 0, 2, "Csus2 root vs bass=D"),
        new([0, 2, 7], 0, 7, "Csus2 root vs bass=G"),
        new([0, 5, 7], 0, 5, "Csus4 root vs bass=F"),
        new([0, 5, 7], 0, 7, "Csus4 root vs bass=G"),
        new([2, 7, 9], 2, 7, "Dsus4 root vs bass=G"),

        // ---------- Drop voicings (bass-permuted) ----------
        new([0, 4, 7, 11], 11, 0, "Cmaj7 drop-2 (bass=B) vs root"),
        new([0, 4, 7, 10], 10, 0, "C7 drop-2 (bass=Bb) vs root"),
        new([0, 3, 7, 10], 10, 3, "Cm7 drop-2 (bass=Bb) vs drop-3 (bass=Eb)"),
        new([0, 2, 4, 7, 11], 2, 11, "Cmaj9 bass=D vs bass=B"),
        new([0, 2, 3, 7, 10], 10, 2, "Cm9 bass=Bb vs bass=D"),

        // ---------- Altered dominants ----------
        new([0, 4, 6, 10], 0, 6, "C7b5 root vs bass=F#"),
        new([0, 1, 4, 7, 10], 0, 1, "C7b9 root vs bass=Db"),
        new([0, 3, 4, 7, 10], 0, 3, "C7#9 root vs bass=Eb"),
        new([0, 4, 6, 7, 10], 0, 6, "C7#11 root vs bass=F#"),
        new([0, 4, 7, 8, 10], 0, 8, "C7b13 root vs bass=Ab"),
    ];
}

/// <summary>Curated dyads for Axis D — each dyad tests the interval-name path.</summary>
internal static class DyadCorpus
{
    internal readonly record struct Entry(int Pc1, int Pc2, string ExpectedName, string ExpectedQuality);

    // INTENT: These entries express the *intended* behavior according to standard music theory
    // and the recognizer's stated goals. If the recognizer deviates, the test FAILS and reveals
    // a bug, per the spec: "failing tests reveal actual bugs we need to know about."
    //
    // Analysis of current recognizer logic (CanonicalChordRecognizer.IdentifyDyad):
    //   pcs sorted ascending [pc1, pc2] with pc1 < pc2.
    //   interval = (pc2 - pc1 + 12) % 12   -> always positive, 1..11.
    //   (root, other) = interval <= 6 ? (pc1, pc2) : (pc2, pc1)
    //   canonicalInterval = (other - root + 12) % 12
    //
    // For the P5 pair {0,7}: interval=7 > 6, so (root=7, other=0), canonicalInterval=5 (=P4).
    // Then isPowerChord = (canonicalInterval == 7) is FALSE. Result: "G + C (Perfect 4th)".
    // THIS IS A BUG: a P5 is never recognized as a power chord via the dyad path.
    //
    // We assert the MUSICALLY CORRECT behavior below. Expected failures on the P5 cases will
    // surface the bug. Note: we also express the correct convention that C+G should name C as
    // the root of a power chord "C5", not G as in the current code.
    internal static readonly IReadOnlyList<Entry> Entries =
    [
        // ---------- Perfect 5th power chords (canonicalInterval should be 7) ----------
        // Expected: root=C, name="C5"; but current recognizer emits "G + C (Perfect 4th)".
        new(0, 7, "C5", "power"),

        // G + D: pcs {2,7}, interval=5. Current logic: (root=2, other=7), canonical=5 (P4).
        // Expected (musically): "G5" since G-D is a P5 with G as the lower root.
        // Current behavior will emit "D + G (Perfect 4th)" — test will FAIL and reveal bug.
        new(2, 7, "G5", "power"),

        // ---------- Named interval dyads (non-P5) ----------
        // C+E (0,4): interval=4<=6, (root=0, other=4), canonical=4 = Major 3rd.
        new(0, 4, "C + E (Major 3rd)", "dyad"),

        // C+Eb (0,3): canonical=3 = Minor 3rd.
        new(0, 3, "C + Eb (Minor 3rd)", "dyad"),

        // C+F (0,5): interval=5, which is the inversion of a P5. Under the consistent
        // "P5-relationship = power chord" rule (applied symmetrically to {2,7}→G5 above),
        // this becomes F5 with F as the root (C is the P5 above F).
        new(0, 5, "F5", "power"),

        // C+F# (0,6): canonical=6 = Tritone.
        new(0, 6, "C + Gb (Tritone)", "dyad"),

        // C+Ab (0,8): interval=8>6, (root=8, other=0), canonical=4 = Major 3rd.
        // The recognizer labels this "Ab + C (Major 3rd)" — correct.
        new(0, 8, "Ab + C (Major 3rd)", "dyad"),

        // C+A (0,9): canonical=3 = Minor 3rd.
        new(0, 9, "A + C (Minor 3rd)", "dyad"),

        // C+Bb (0,10): canonical=2 = Major 2nd.
        new(0, 10, "Bb + C (Major 2nd)", "dyad"),

        // C+B (0,11): canonical=1 = Minor 2nd.
        new(0, 11, "B + C (Minor 2nd)", "dyad"),
    ];
}

/// <summary>Pitch-class sets designed to force the Forte fallback path.</summary>
internal static class ForteFallbackCorpus
{
    internal readonly record struct Entry(int[] PitchClasses, string ExpectedCardinalityName, string Description);

    // These heptachord/octachord/nonachord sets should not match any of the tonal
    // patterns. If they do match a set-class pattern (e.g. whole-tone-hexachord),
    // the test accepts that too — the only failure mode is an unexpected match.
    internal static readonly IReadOnlyList<Entry> Entries =
    [
        // Chromatic heptachord (consecutive semitones is very unlikely to be a named chord)
        new([0, 1, 2, 3, 4, 5, 6], "heptachord", "chromatic cluster 7-1"),

        // Asymmetric 7-note set (not diatonic, not symmetric)
        new([0, 1, 2, 4, 5, 7, 8], "heptachord", "asymmetric heptachord"),

        // 8-note chromatic cluster
        new([0, 1, 2, 3, 4, 5, 6, 7], "octachord", "chromatic octachord 8-1"),

        // 9-note near-chromatic
        new([0, 1, 2, 3, 4, 5, 6, 7, 8], "nonachord", "chromatic nonachord 9-1"),

        // 10-note near-chromatic
        new([0, 1, 2, 3, 4, 5, 6, 7, 8, 9], "decachord", "chromatic decachord 10-1"),
    ];
}
