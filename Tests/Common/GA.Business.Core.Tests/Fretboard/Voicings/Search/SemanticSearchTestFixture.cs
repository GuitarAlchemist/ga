namespace GA.Business.Core.Tests.Fretboard.Voicings.Search;

using System.IO;
using System.Reflection;
using GA.Business.Core.Fretboard.Voicings.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

[SetUpFixture]
public class SemanticSearchTestFixture
{
    public static EnhancedVoicingSearchService SearchService { get; private set; }
    public static VoicingIndexingService IndexingService { get; private set; }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // 1. Initialize Core Services
        IndexingService = new VoicingIndexingService();
        
        // 2. Setup dependency mocks
        // We use CpuVoicingSearchStrategy for stability in tests
        var strategy = new CpuVoicingSearchStrategy();
        
        SearchService = new EnhancedVoicingSearchService(
            IndexingService,
            strategy
        );
        
        // 3. Load Data from Binary Cache
        var cachePath = FindVoicingCache();
        
        if (File.Exists(cachePath))
        {
            TestContext.Progress.WriteLine($"Loading voicings from: {cachePath}");
            var voicings = VoicingCacheSerialization.LoadFromCache(cachePath);
            IndexingService.LoadDocuments(voicings);
            TestContext.Progress.WriteLine($"Loaded {voicings.Count} voicings for testing.");
        }
        else
        {
            TestContext.Progress.WriteLine("Cache file not found. Generating voicings... (This may take a moment)");
            var voicings = GenerateAndCacheVoicings(cachePath);
            IndexingService.LoadDocuments(voicings);
            TestContext.Progress.WriteLine($"Generated and loaded {voicings.Count} voicings.");
        }

        // Initialize with basic mock embeddings to allow testing of semantic matching logic
        // We map keywords to specific dimensions to simulate semantic relatedness
        SearchService.InitializeEmbeddingsAsync(text => Task.FromResult(MockEmbed(text))).GetAwaiter().GetResult();
    }
    
    public static double[] MockEmbed(string text)
    {
        var vector = new double[768];
        var lower = text.ToLowerInvariant();
        
        if (lower.Contains("sad") || lower.Contains("minor") || lower.Contains("melancholy")) { vector[0] = 1.0; }
        if (lower.Contains("happy") || lower.Contains("major")) { vector[1] = 1.0; } // Removed 'dreamy' to separate it
        if (lower.Contains("hendrix") || lower.Contains("7#9")) { vector[2] = 1.0; } // Removed 'tension' to separate it
        if (lower.Contains("jazz") || lower.Contains("complex") || lower.Contains("13")) { vector[3] = 1.0; }
        if (lower.Contains("open") || lower.Contains("beginner") || lower.Contains("campfire")) { vector[4] = 1.0; }
        if (lower.Contains("barre") || lower.Contains("difficult")) { vector[5] = 1.0; }
        if (lower.Contains("common") || lower.Contains("standard") || lower.Contains("triad")) { vector[6] = 1.0; }
        
        // New Canonical Dimensions
        if (lower.Contains("bond") || lower.Contains("spy") || lower.Contains("mystery") || lower.Contains("noir")) { vector[7] = 1.0; }
        if (lower.Contains("mu") || lower.Contains("steely") || lower.Contains("add2") || lower.Contains("add9")) { vector[8] = 1.0; }
        if (lower.Contains("soul") || lower.Contains("r&b") || lower.Contains("neo")) { vector[9] = 1.0; }
        if (lower.Contains("flamenco") || lower.Contains("spanish") || lower.Contains("phrygian")) { vector[10] = 1.0; }
        if (lower.Contains("quartal") || lower.Contains("modal") || lower.Contains("so what")) { vector[11] = 1.0; }
        if (lower.Contains("dreamy") || lower.Contains("essential") || lower.Contains("wonder")) { vector[12] = 1.0; }
        if (lower.Contains("tense") || lower.Contains("dissonant") || lower.Contains("diminished")) { vector[13] = 1.0; }
        
        // Phase 2: Structural & Timbral Dimensions
        if (lower.Contains("aggressive") || lower.Contains("power") || lower.Contains("distortion")) { vector[14] = 1.0; }
        if (lower.Contains("low") || lower.Contains("deep") || lower.Contains("heavy") || lower.Contains("dark")) { vector[15] = 1.0; }
        if (lower.Contains("high") || lower.Contains("luminous") || lower.Contains("sparkle") || lower.Contains("bright")) { vector[16] = 1.0; } // Merged bright into high/luminous for simplicity or keep separate? distinct is better.
        // Actually let's keep bright separate as per nomenclature if possible, but "bright" often overlaps high. 
        // Let's make 16 generic "High/Bright" for now or stick to nomenclature.
        // Nomenclature: Bright -> id:bright (Lydian, Major). High -> id:register:high.
        // Let's separate for precision if keywords allow.
        
        if (lower.Contains("high") || lower.Contains("luminous") || lower.Contains("upper")) { vector[16] = 1.0; }
        if (lower.Contains("shell") || lower.Contains("guide")) { vector[17] = 1.0; }
        if (lower.Contains("rootless")) { vector[18] = 1.0; }
        if (lower.Contains("dense") || lower.Contains("cluster") || lower.Contains("closed")) { vector[19] = 1.0; }
        if (lower.Contains("stable") || lower.Contains("grounded")) { vector[20] = 1.0; }
        if (lower.Contains("bright") || lower.Contains("lydian") || lower.Contains("lift")) { vector[21] = 1.0; }

        return vector;
    }

    private List<VoicingDocument> GenerateAndCacheVoicings(string cachePath)
    {
        // Simpler approach: Create a Mock/Synthetic set of documents for testing if cache is missing.
        // This avoids the heavy dependency on the generator logic in the test fixture.
        
        var documents = new List<VoicingDocument>();
        
        // Helper to create valid dummy document
        VoicingDocument CreateDoc(string id, string name, string[] tags, string text, string difficulty = "Intermediate", string position = "Open", int minFret = 0, string mode = "Major", 
            string? stacking = "Tertian", int rootPc = 0, int bassNote = 0, int[]? midiNotes = null,
            // Phase 3 Args
            string harmonicFunction = "Tonic", bool isNatural = true, bool isRootless = false, bool hasGuideTones = true, int inversion = 0, 
            double consonance = 0.8, double brightness = 0.5, string[]? omitted = null,
            // Agent Args
            string? texture = null, string[]? doubled = null, string[]? altNames = null)
        {
            return new VoicingDocument
            {
                Id = id,
                ChordName = name,
                SemanticTags = tags,
                SearchableText = text,
                Difficulty = difficulty,
                Position = position,
                MinFret = minFret,
                ModeName = mode,
                
                // Structured Fields
                StackingType = stacking,
                RootPitchClass = rootPc,
                MidiBassNote = bassNote,
                MidiNotes = midiNotes ?? [40, 44, 47], // Default to low E major approx
                
                // Phase 3 Fields
                HarmonicFunction = harmonicFunction,
                IsNaturallyOccurring = isNatural,
                IsRootless = isRootless,
                HasGuideTones = hasGuideTones,
                Inversion = inversion,
                Consonance = consonance,
                Brightness = brightness,
                OmittedTones = omitted ?? [],
                
                // Required dummy fields
                YamlAnalysis = "{}",
                Diagram = "x32010", 
                PitchClassSet = "{0,4,7}",
                PitchClasses = [0, 4, 7],
                IntervalClassVector = "000000",
                
                // Agent Metadata
                TexturalDescription = texture,
                DoubledTones = doubled,
                AlternateNames = altNames,

                AnalysisEngine = "Test",
                AnalysisVersion = "1.0",
                Jobs = [],
                PossibleKeys = [],
                TuningId = "Standard",
                PitchClassSetId = "0",
                DifficultyScore = 1.0
            };
        }

        // Add "Sad" C Minor
        documents.Add(CreateDoc("test-Cm", "C Minor", ["sad", "melancholy", "minor"], "C Minor sad melancholy", "Beginner", "Open", 0, "Minor", "Tertian", 0, 48, [48, 51, 55]));
        
         // Add "Hendrix" E7#9
        documents.Add(CreateDoc("test-E7s9", "E 7#9", ["hendrix", "psychedelic", "tension"], "E 7#9 hendrix", "Advanced", "Middle", 6, "Mixolydian"));
        
         // Add "Fast Car" Cmaj7
        documents.Add(CreateDoc("test-Cmaj7", "C Major 7", ["happy", "dreamy", "major"], "C Major 7 happy", "Beginner", "Open", 0, "Major"));

        // Add Open G
        documents.Add(CreateDoc("test-G", "G Major", ["happy", "campfire", "major", "common"], "G Major happy open common", "Beginner", "Open", 0, "Major"));
        
        // Add Open D
        documents.Add(CreateDoc("test-D", "D Major", ["happy", "major", "common"], "D Major happy open common", "Beginner", "Open", 0, "Major"));
        
        // Add Open Em (Sad Open)
        documents.Add(CreateDoc("test-Em", "E Minor", ["sad", "minor", "common"], "E Minor sad open common", "Beginner", "Open", 0, "Minor"));

        // Add Barre F (Happy Barre)
        documents.Add(CreateDoc("test-F-barre", "F Major", ["happy", "major", "barre"], "F Major happy barre", "Intermediate", "Barre", 1, "Major"));

        // Add Barre Bm (Sad Barre)
        documents.Add(CreateDoc("test-Bm-barre", "B Minor", ["sad", "minor", "barre"], "B Minor sad barre", "Intermediate", "Barre", 2, "Minor"));
        
        // Add Jazzy C13
        documents.Add(CreateDoc("test-C13", "C 13", ["jazz", "complex", "dominant", "soulful"], "C 13 jazzy soulful", harmonicFunction: "Dominant"));

        // -- Canonical Additions --

        // James Bond Chord (E mMaj9)
        documents.Add(CreateDoc("test-Bond", "E Minor Major 9", ["james-bond-chord", "spy", "mystery", "noir"], "E mMaj9 james bond mystery spy"));

        // Mu Major (G add9)
        documents.Add(CreateDoc("test-Mu", "G Major Add 9", ["mu-major", "steely", "add9"], "G add9 mu major steely"));

        // Neo-Soul (Eb m9)
        documents.Add(CreateDoc("test-NeoSoul", "Eb Minor 9", ["neo-soul", "r&b", "soulful"], "Eb m9 neo soul r&b"));

        // Flamenco (E 7b9)
        documents.Add(CreateDoc("test-Flamenco", "E 7b9", ["flamenco", "spanish", "phrygian", "tense"], "E 7b9 flamenco spanish phrygian tense"));

        // So What (D m11) - QUARTAL
        documents.Add(CreateDoc("test-SoWhat", "D Minor 11", ["so-what-chord", "quartal", "modal"], "D m11 so what quartal modal", "Advanced", "Middle", 5, "Dorian", "Quartal"));

        // Dreamy (C Lydian/Maj7#11)
        documents.Add(CreateDoc("test-Dreamy", "C Major 7#11", ["dreamy", "lydian", "wonder"], "C Maj7#11 dreamy lydian wonder", brightness: 0.9));

        // Tense (B dim7)
        documents.Add(CreateDoc("test-Tense", "B Diminished 7", ["tense", "dissonant", "diminished"], "B dim7 tense dissonant", consonance: 0.2));

        // -- Phase 2: Structural & Timbral --

        // Aggressive (Power Chords)
        documents.Add(CreateDoc("test-Aggressive", "E5 Power Chord", ["aggressive", "power", "distortion"], "E5 aggressive power distortion", harmonicFunction: "Tonic", omitted: ["3rd", "7th"]));

        // Low Register
        documents.Add(CreateDoc("test-Low", "Low E Major", ["low", "deep", "heavy", "dark"], "Low E Major deep heavy dark", "Beginner", "Open", 0, "Major", "Tertian", 4, 40, [40, 47, 52, 56, 59, 64])); // Low E range

        // High Register
        documents.Add(CreateDoc("test-High", "High G Major", ["high", "luminous", "sparkle"], "High G Major luminous sparkle upper", "Advanced", "High", 12, "Major", "Tertian", 7, 79, [79, 83, 86, 91])); // High range

        // Shell Voicing
        documents.Add(CreateDoc("test-Shell", "C Maj7 Shell", ["shell", "guide", "jazz"], "C Maj7 Shell guide tones", omitted: ["5th", "Root"]));

        // Rootless Voicing
        documents.Add(CreateDoc("test-Rootless", "C Maj9 Rootless", ["rootless", "jazz", "complex"], "C Maj9 Rootless jazz", isRootless: true, omitted: ["Root"]));

        // Dense/Cluster
        documents.Add(CreateDoc("test-Dense", "C Add2 Closed", ["dense", "cluster", "closed"], "C Add2 dense cluster closed", "Intermediate", "Closed", 0, "Major", "Secundal", consonance: 0.4));

        // Stable
        documents.Add(CreateDoc("test-Stable", "C Major Triad", ["stable", "grounded", "major"], "C Major stable grounded", consonance: 1.0));

        // Bright
        documents.Add(CreateDoc("test-Bright", "C Lydian", ["bright", "lydian", "lift"], "C Lydian bright lift", brightness: 0.95));

        // Slash Chord (New) for Structured Tests
        documents.Add(CreateDoc("test-Slash", "C/G", ["inversion", "slash"], "C over G slash chord", "Beginner", "Open", 0, "Major", "Tertian", 0, 43, inversion: 2)); // C Root(0), Bass G(43=7%12), 2nd Inversion
        
        // -- Phase 3: Extra Synthetic Docs --
        
        // Dominant G7
        documents.Add(CreateDoc("test-G7", "G7", ["dominant", "blues"], "G7 dominant blues", harmonicFunction: "Dominant", hasGuideTones: true));
        
        // Non-Diatonic
        documents.Add(CreateDoc("test-Chromatic", "Db Maj7", ["chromatic", "out"], "Db Maj7 out", isNatural: false));
        
        // Non-Diatonic
        documents.Add(CreateDoc("test-Chromatic", "Db Maj7", ["chromatic", "out"], "Db Maj7 out", isNatural: false));
        
        // -- Agent Metadata Tests --
        
        // Texture
        documents.Add(CreateDoc("test-Warm", "C Major Warm", ["warm", "lush"], "C Major warm lush", texture: "Warm and lush"));

        // Alt Names (C6)
        documents.Add(CreateDoc("test-C6", "C Major 6", ["c6", "major6", "jazz"], "C Major 6 jazz", altNames: ["Am7", "C6", "A Minor 7"]));

        return documents;
    }

    private string FindVoicingCache()
    {
        // Search significantly upwards because tests run in a deep subdir
        var searchPaths = new[]
        {
            "cache/indexes/voicings_v1.bin",
            "../../../Apps/ga-server/GaApi/cache/indexes/voicings_v1.bin",
            "../../../../Apps/ga-server/GaApi/cache/indexes/voicings_v1.bin",
            "../../../../../Apps/ga-server/GaApi/cache/indexes/voicings_v1.bin", // Deep debug path
            "../../../../../../Apps/ga-server/GaApi/cache/indexes/voicings_v1.bin",
            Path.Combine(TestContext.CurrentContext.TestDirectory, "voicings_v1.bin"),
            "C:\\Users\\spare\\source\\repos\\ga\\Apps\\ga-server\\GaApi\\cache\\indexes\\voicings_v1.bin" // Hard fallback
        };

        foreach (var path in searchPaths)
        {
            var abs = Path.GetFullPath(path);
            if (File.Exists(abs)) return abs;
        }
        
        return "voicings_v1.bin";
    }
}
