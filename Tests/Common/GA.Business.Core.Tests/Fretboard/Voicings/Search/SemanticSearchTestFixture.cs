namespace GA.Business.Core.Tests.Fretboard.Voicings.Search;

using GA.Business.ML.Search;
using GA.Business.ML.Rag.Models;
using NUnit.Framework;

/// <summary>
/// Test fixture for semantic/structured voicing search tests.
/// Always uses a deterministic set of synthetic mock documents so that
/// test assertions are stable regardless of whether a production voicing
/// cache is present on the machine.
/// </summary>
[SetUpFixture]
public class SemanticSearchTestFixture
{
    public static EnhancedVoicingSearchService SearchService { get; private set; } = null!;
    public static VoicingIndexingService IndexingService { get; private set; } = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // 1. Initialize Core Services
        IndexingService = new VoicingIndexingService();

        // Use CpuVoicingSearchStrategy for stability in tests
        var strategy = new CpuVoicingSearchStrategy();
        SearchService = new EnhancedVoicingSearchService(IndexingService, strategy);

        // 2. Always load the deterministic synthetic mock document set.
        //    We intentionally skip the production binary cache here because:
        //    - The cache contains real (scraped) voicings without the specific
        //      metadata (Id, ChordName, SemanticTags) that the assertions rely on.
        //    - This makes the tests stable and fast on any machine.
        var syntheticDocs = BuildSyntheticDocuments();
        IndexingService.LoadDocuments(syntheticDocs);
        TestContext.Progress.WriteLine($"[Fixture] Loaded {syntheticDocs.Count} synthetic mock documents.");

        // 3. Initialize embeddings using the mock embedding function.
        SearchService.InitializeEmbeddingsAsync(
            text => Task.FromResult(MockEmbed(text))
        ).GetAwaiter().GetResult();

        TestContext.Progress.WriteLine("[Fixture] Search service initialized.");
    }

    // ---------------------------------------------------------------------------
    // Mock embedding: maps keywords → specific dimensions so that cosine similarity
    // between a query and a document's text is deterministic and meaningful.
    // ---------------------------------------------------------------------------
    public static double[] MockEmbed(string text)
    {
        var vector = new double[768];
        var lower = text.ToLowerInvariant();

        // Emotional / harmonic colour
        if (lower.Contains("sad") || lower.Contains("minor") || lower.Contains("melancholy"))   vector[0] = 1.0;
        if (lower.Contains("happy") || lower.Contains("major"))                                  vector[1] = 1.0;
        if (lower.Contains("hendrix") || lower.Contains("7#9"))                                  vector[2] = 1.0;
        if (lower.Contains("jazz") || lower.Contains("complex") || lower.Contains("13"))         vector[3] = 1.0;
        if (lower.Contains("open") || lower.Contains("beginner") || lower.Contains("campfire"))  vector[4] = 1.0;
        if (lower.Contains("barre") || lower.Contains("difficult"))                              vector[5] = 1.0;
        if (lower.Contains("common") || lower.Contains("standard") || lower.Contains("triad"))   vector[6] = 1.0;

        // Canonical named-voicing dimensions
        if (lower.Contains("bond") || lower.Contains("spy") || lower.Contains("mystery") || lower.Contains("noir"))  vector[7] = 1.0;
        if (lower.Contains("mu") || lower.Contains("steely") || lower.Contains("add2") || lower.Contains("add9"))    vector[8] = 1.0;
        if (lower.Contains("soul") || lower.Contains("r&b") || lower.Contains("neo"))                                vector[9] = 1.0;
        if (lower.Contains("flamenco") || lower.Contains("spanish") || lower.Contains("phrygian"))                   vector[10] = 1.0;
        if (lower.Contains("quartal") || lower.Contains("modal") || lower.Contains("so what"))                       vector[11] = 1.0;
        if (lower.Contains("dreamy") || lower.Contains("wonder"))                                                    vector[12] = 1.0;
        if (lower.Contains("tense") || lower.Contains("dissonant") || lower.Contains("diminished"))                  vector[13] = 1.0;

        // Phase 2: Structural / timbral
        if (lower.Contains("aggressive") || lower.Contains("power") || lower.Contains("distortion"))                 vector[14] = 1.0;
        if (lower.Contains("low") || lower.Contains("deep") || lower.Contains("heavy") || lower.Contains("dark"))   vector[15] = 1.0;
        if (lower.Contains("high") || lower.Contains("luminous") || lower.Contains("sparkle") || lower.Contains("upper")) vector[16] = 1.0;
        if (lower.Contains("shell") || lower.Contains("guide"))                                                      vector[17] = 1.0;
        if (lower.Contains("rootless"))                                                                              vector[18] = 1.0;
        if (lower.Contains("dense") || lower.Contains("cluster") || lower.Contains("closed"))                       vector[19] = 1.0;
        if (lower.Contains("stable") || lower.Contains("grounded"))                                                  vector[20] = 1.0;
        if (lower.Contains("bright") || lower.Contains("lydian") || lower.Contains("lift"))                         vector[21] = 1.0;
        if (lower.Contains("dominant") || lower.Contains("blues"))                                                   vector[22] = 1.0;
        if (lower.Contains("slash") || lower.Contains("inversion"))                                                  vector[23] = 1.0;
        if (lower.Contains("warm") || lower.Contains("lush"))                                                        vector[24] = 1.0;

        return vector;
    }

    // ---------------------------------------------------------------------------
    // Synthetic document set — every document has rich, deterministic metadata so
    // all structured and semantic filter assertions can pass.
    // ---------------------------------------------------------------------------
    private static List<ChordVoicingRagDocument> BuildSyntheticDocuments()
    {
        var docs = new List<ChordVoicingRagDocument>();

        // ── Helper ──────────────────────────────────────────────────────────────
        ChordVoicingRagDocument D(
            string id, string name, string[] tags, string searchText,
            string difficulty = "Intermediate",
            string position = "Open",
            int minFret = 0,
            string mode = "Major",
            string stacking = "Tertian",
            int rootPc = 0,
            int bassNote = 0,
            int[]? midi = null,
            string harmonicFn = "Tonic",
            bool isNatural = true,
            bool isRootless = false,
            bool hasGuideTones = false,
            int inversion = 0,
            double consonance = 0.8,
            double brightness = 0.5,
            string[]? omitted = null,
            string? texture = null,
            string[]? altNames = null,
            int topPc = 4,
            string diagram = "x32010")
        {
            return new ChordVoicingRagDocument
            {
                Id = id,
                ChordName = name,
                SemanticTags = tags,
                SearchableText = searchText,
                Difficulty = difficulty,
                Position = position,
                MinFret = minFret,
                ModeName = mode,
                StackingType = stacking,
                RootPitchClass = rootPc,
                MidiBassNote = bassNote,
                MidiNotes = midi ?? [40, 44, 47],
                HarmonicFunction = harmonicFn,
                IsNaturallyOccurring = isNatural,
                IsRootless = isRootless,
                HasGuideTones = hasGuideTones,
                Inversion = inversion,
                Consonance = consonance,
                Brightness = brightness,
                OmittedTones = omitted ?? [],
                YamlAnalysis = "{}",
                Diagram = diagram,
                PitchClassSet = "{0,4,7}",
                PitchClasses = [0, 4, 7],
                IntervalClassVector = "000000",
                TexturalDescription = texture,
                AlternateNames = altNames ?? [],
                AnalysisEngine = "Test",
                AnalysisVersion = "1.0",
                Jobs = [],
                PossibleKeys = [],
                TuningId = "Standard",
                PitchClassSetId = "0",
                DifficultyScore = 1.0,
                TopPitchClass = topPc
            };
        }
        // ────────────────────────────────────────────────────────────────────────

        // ── Phase 1: Emotional / harmonic ──
        docs.Add(D("test-Cm",      "C Minor",        ["sad", "melancholy", "minor"],
            "C Minor sad melancholy", "Beginner", "Open", 0, "Minor", "Tertian", 0, 48, [48, 51, 55], consonance: 0.6));

        docs.Add(D("test-E7s9",    "E 7#9",          ["hendrix", "psychedelic", "tension"],
            "E 7#9 hendrix", "Advanced", "Middle", 6, "Mixolydian"));

        docs.Add(D("test-Cmaj7",   "C Major 7",      ["happy", "dreamy", "major"],
            "C Major 7 happy", "Beginner", "Open", 0, "Major"));

        docs.Add(D("test-G",       "G Major",        ["happy", "campfire", "major", "common"],
            "G Major happy open common", "Beginner", "Open", 0, "Major"));

        docs.Add(D("test-D",       "D Major",        ["happy", "major", "common"],
            "D Major happy open common", "Beginner", "Open", 0, "Major"));

        docs.Add(D("test-Em",      "E Minor",        ["sad", "minor", "common"],
            "E Minor sad open common", "Beginner", "Open", 0, "Minor"));

        docs.Add(D("test-F-barre", "F Major",        ["happy", "major", "barre"],
            "F Major happy barre", "Intermediate", "Barre", 1, "Major"));

        docs.Add(D("test-Bm-barre","B Minor",        ["sad", "minor", "barre"],
            "B Minor sad barre", "Intermediate", "Barre", 2, "Minor"));

        // ── Phase 1: Jazz / complex ──
        docs.Add(D("test-C13",     "C 13",           ["jazz", "complex", "dominant", "soulful"],
            "C 13 jazzy soulful dominant blues", harmonicFn: "Dominant", hasGuideTones: true, consonance: 0.7));

        // ── Canonical named voicings ──
        docs.Add(D("test-Bond",    "E Minor Major 9",["james-bond-chord", "spy", "mystery", "noir"],
            "E mMaj9 james bond mystery spy noir"));

        docs.Add(D("test-Mu",      "G Major Add 9",  ["mu-major", "steely", "add9"],
            "G add9 mu major steely add2"));

        docs.Add(D("test-NeoSoul", "Eb Minor 9",     ["neo-soul", "r&b", "soulful"],
            "Eb m9 neo soul r&b"));

        docs.Add(D("test-Flamenco","E 7b9",          ["flamenco", "spanish", "phrygian", "tense"],
            "E 7b9 flamenco spanish phrygian tense"));

        docs.Add(D("test-SoWhat",  "D Minor 11",     ["so-what-chord", "quartal", "modal"],
            "D m11 so what quartal modal", "Advanced", "Middle", 5, "Dorian", "Quartal"));

        docs.Add(D("test-Dreamy",  "C Major 7#11",   ["dreamy", "lydian", "wonder"],
            "C Maj7#11 dreamy lydian wonder", brightness: 0.9));

        docs.Add(D("test-Tense",   "B Diminished 7", ["tense", "dissonant", "diminished"],
            "B dim7 tense dissonant diminished", consonance: 0.2));

        // ── Phase 2: Structural / timbral ──
        docs.Add(D("test-Aggressive","E5 Power Chord",["aggressive", "power", "distortion"],
            "E5 aggressive power distortion", harmonicFn: "Tonic", omitted: ["3rd", "7th"]));

        docs.Add(D("test-Low",     "Low E Major",    ["low", "deep", "heavy", "dark"],
            "Low E Major deep heavy dark", "Beginner", "Open", 0, "Major", "Tertian",
            4, 40, [40, 47, 52, 56, 59, 64]));

        docs.Add(D("test-High",    "High G Major",   ["high", "luminous", "sparkle"],
            "High G Major luminous sparkle upper", "Advanced", "High", 12, "Major", "Tertian",
            7, 79, [79, 83, 86, 91], brightness: 0.95));

        docs.Add(D("test-Shell",   "C Maj7 Shell",   ["shell", "guide", "jazz"],
            "C Maj7 Shell guide tones", hasGuideTones: true, omitted: ["5th", "Root"]));

        docs.Add(D("test-Rootless","C Maj9 Rootless",["rootless", "jazz", "complex"],
            "C Maj9 Rootless jazz", isRootless: true, omitted: ["Root"]));

        docs.Add(D("test-Dense",   "C Add2 Closed",  ["dense", "cluster", "closed"],
            "C Add2 dense cluster closed", "Intermediate", "Closed", 0, "Major", "Secundal",
            consonance: 0.4));

        docs.Add(D("test-Stable",  "C Major Triad",  ["stable", "grounded", "major"],
            "C Major stable grounded", consonance: 1.0));

        docs.Add(D("test-Bright",  "C Lydian",       ["bright", "lydian", "lift"],
            "C Lydian bright lift", brightness: 0.95));

        // ── Phase 3: Structured filter targets ──

        // Slash chord — Inversion = 2, IsSlashChord detected via "/" in name
        docs.Add(D("test-Slash",   "C/G",            ["inversion", "slash"],
            "C over G slash chord inversion", "Beginner", "Open", 0, "Major", "Tertian",
            0, 43, [43, 48, 52, 55], inversion: 2, diagram: "3-2-0-0-1-0"));

        // Dominant G7
        docs.Add(D("test-G7",     "G7",              ["dominant", "blues"],
            "G7 dominant blues", harmonicFn: "Dominant", hasGuideTones: true));

        // Non-diatonic (IsNaturallyOccurring = false)
        docs.Add(D("test-Chromatic","Db Maj7",       ["chromatic", "out"],
            "Db Maj7 out", isNatural: false));

        // Agent metadata tests
        docs.Add(D("test-Warm",   "C Major Warm",    ["warm", "lush"],
            "C Major warm lush", texture: "Warm and lush"));

        // C6 / Am7 enharmonic equivalents
        docs.Add(D("test-C6",     "C Major 6",       ["c6", "major6", "jazz"],
            "C Major 6 jazz", altNames: ["Am7", "C6", "A Minor 7"]));

        return docs;
    }
}
