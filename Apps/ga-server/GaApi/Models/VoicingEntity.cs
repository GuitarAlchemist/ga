namespace GaApi.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// MongoDB entity representing a specific guitar voicing (physical shape) on the fretboard.
/// This persists comprehensive analysis data for chatbot retrieval, organized in 3 layers:
/// - Identity: pitch-class set, chord candidates, function candidates
/// - Sound: register, spacing, doublings, perceptual qualities
/// - Hands: positions, fingerings, ergonomics, transition costs
/// </summary>
[BsonIgnoreExtraElements]
public class VoicingEntity
{
    // ========== Core Identification ==========

    /// <summary>
    /// MongoDB internal ID
    /// </summary>
    [BsonId]
    public ObjectId MongoId { get; set; }

    /// <summary>
    /// Unique textual identifier for the voicing (e.g., "voicing_x_3_2_0_1_0")
    /// </summary>
    [BsonElement("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The visual fretboard diagram string (e.g., "x-3-2-0-1-0")
    /// </summary>
    [BsonElement("diagram")]
    public required string Diagram { get; set; }

    /// <summary>
    /// Searchable text blob describing the voicing (for full-text search)
    /// </summary>
    [BsonElement("searchableText")]
    public string? SearchableText { get; set; }

    // ========== LAYER 1: IDENTITY (Pitch-class set, chord candidates, function candidates) ==========

    /// <summary>
    /// Primary chord name (e.g., "C Major", "Dm7")
    /// </summary>
    [BsonElement("chordName")]
    public string? ChordName { get; set; }

    /// <summary>
    /// Alternative chord interpretations (ambiguity tracking).
    /// E.g., C6 can also be Am7. Stores candidate names with confidence.
    /// </summary>
    [BsonElement("alternateChordNames")]
    public string[]? AlternateChordNames { get; set; }

    /// <summary>
    /// Indicates this voicing is ambiguous and requires harmonic context to interpret.
    /// E.g., sus chords, quartal voicings, polychords.
    /// </summary>
    [BsonElement("requiresContext")]
    public bool RequiresContext { get; set; }

    /// <summary>
    /// Array of MIDI note values (actual sounding pitches)
    /// </summary>
    [BsonElement("midiNotes")]
    public int[] MidiNotes { get; set; } = [];

    /// <summary>
    /// Array of unique pitch classes (0-11)
    /// </summary>
    [BsonElement("pitchClasses")]
    public int[] PitchClasses { get; set; } = [];

    /// <summary>
    /// Canonical Prime Form ID (e.g., "024") for finding "cousin" chords.
    /// </summary>
    [BsonElement("primeFormId")]
    public string? PrimeFormId { get; set; }

    /// <summary>
    /// Forte Number (e.g., "3-11" for Minor/Major triad) for set-theory search.
    /// </summary>
    [BsonElement("forteCode")]
    public string? ForteCode { get; set; }

    /// <summary>
    /// Interval class vector (e.g., "<254361>")
    /// </summary>
    [BsonElement("intervalClassVector")]
    public string? IntervalClassVector { get; set; }

    /// <summary>
    /// Associated mode name if applicable (e.g., "Dorian", "Mixolydian b6")
    /// </summary>
    [BsonElement("modeName")]
    public string? ModeName { get; set; }

    /// <summary>
    /// Closest diatonic key (e.g., "C Major", "A Minor")
    /// </summary>
    [BsonElement("closestKey")]
    public string? ClosestKey { get; set; }

    /// <summary>
    /// Roman numeral analysis in closest key (e.g., "IVmaj7", "ii7")
    /// </summary>
    [BsonElement("romanNumeral")]
    public string? RomanNumeral { get; set; }

    /// <summary>
    /// Harmonic function (Tonic, Predominant, Dominant, Ambiguous)
    /// </summary>
    [BsonElement("harmonicFunction")]
    public string? HarmonicFunction { get; set; }

    /// <summary>
    /// Whether this chord naturally occurs in its closest key (vs borrowed/chromatic)
    /// </summary>
    [BsonElement("isNaturallyOccurring")]
    public bool IsNaturallyOccurring { get; set; }

    // ========== LAYER 2: SOUND (Register, spacing, doublings, perceptual qualities) ==========

    /// <summary>
    /// Tuning used for this voicing (e.g., "Standard", "Drop D", "DADGAD").
    /// </summary>
    [BsonElement("tuning")]
    public string Tuning { get; set; } = "Standard";

    /// <summary>
    /// Capo position (0 = no capo). Affects sounding pitch vs fretted pitch.
    /// </summary>
    [BsonElement("capoPosition")]
    public int CapoPosition { get; set; } = 0;

    /// <summary>
    /// Voicing type (e.g., "Drop-2", "Drop-3", "Shell", "Closed", "Open", "Spread")
    /// </summary>
    [BsonElement("voicingType")]
    public string? VoicingType { get; set; }

    /// <summary>
    /// Total span in semitones from lowest to highest note
    /// </summary>
    [BsonElement("voicingSpan")]
    public int VoicingSpan { get; set; }

    /// <summary>
    /// Whether this is a rootless voicing (bass note is not the chord root)
    /// </summary>
    [BsonElement("isRootless")]
    public bool IsRootless { get; set; }

    /// <summary>
    /// Tone inventory: which chord tones are present (Root, 3rd, 5th, 7th, 9th, etc.)
    /// </summary>
    [BsonElement("tonesPresent")]
    public string[]? TonesPresent { get; set; }

    /// <summary>
    /// Tones that are doubled in the voicing
    /// </summary>
    [BsonElement("doubledTones")]
    public string[]? DoubledTones { get; set; }

    /// <summary>
    /// Tones that are omitted from the full chord structure
    /// </summary>
    [BsonElement("omittedTones")]
    public string[]? OmittedTones { get; set; }

    /// <summary>
    /// Whether guide tones (3rd + 7th) are present (critical for jazz comping)
    /// </summary>
    [BsonElement("hasGuideTones")]
    public bool HasGuideTones { get; set; }

    /// <summary>
    /// Register classification: "Low", "Mid", "High", "Full Range"
    /// </summary>
    [BsonElement("register")]
    public string? Register { get; set; }

    /// <summary>
    /// Approximate spectral brightness (0.0 = dark/muddy, 1.0 = bright/cutting)
    /// </summary>
    [BsonElement("brightness")]
    public double Brightness { get; set; }

    /// <summary>
    /// Sensory consonance score (0.0 = very dissonant, 1.0 = very consonant)
    /// Based on psychoacoustic roughness/beating analysis
    /// </summary>
    [BsonElement("consonanceScore")]
    public double ConsonanceScore { get; set; }

    /// <summary>
    /// Whether close intervals in low register may cause muddiness
    /// </summary>
    [BsonElement("mayBeMuddy")]
    public bool MayBeMuddy { get; set; }

    // ========== LAYER 3: HANDS (Positions, fingerings, ergonomics, transition costs) ==========

    /// <summary>
    /// Fret position description (e.g., "Open Position", "5th Position")
    /// </summary>
    [BsonElement("handPosition")]
    public string? HandPosition { get; set; }

    /// <summary>
    /// Difficulty rating (Beginner, Intermediate, Advanced, Expert)
    /// </summary>
    [BsonElement("difficulty")]
    public string? Difficulty { get; set; }

    /// <summary>
    /// Lowest fret used (excluding open strings)
    /// </summary>
    [BsonElement("minFret")]
    public int MinFret { get; set; }

    /// <summary>
    /// Highest fret used
    /// </summary>
    [BsonElement("maxFret")]
    public int MaxFret { get; set; }

    /// <summary>
    /// Number of frets spanned by the hand
    /// </summary>
    [BsonElement("handStretch")]
    public int HandStretch { get; set; }

    /// <summary>
    /// Whether the voicing requires a barre
    /// </summary>
    [BsonElement("barreRequired")]
    public bool BarreRequired { get; set; }

    /// <summary>
    /// Barre details if applicable (finger, fret, strings covered)
    /// </summary>
    [BsonElement("barreInfo")]
    public string? BarreInfo { get; set; }

    /// <summary>
    /// Suggested finger assignment (1=index, 2=middle, 3=ring, 4=pinky, T=thumb)
    /// </summary>
    [BsonElement("fingerAssignment")]
    public string? FingerAssignment { get; set; }

    /// <summary>
    /// Number of string skips (harder for fast playing)
    /// </summary>
    [BsonElement("stringSkips")]
    public int StringSkips { get; set; }

    /// <summary>
    /// Minimum fingers required to play this voicing
    /// </summary>
    [BsonElement("minimumFingers")]
    public int MinimumFingers { get; set; }

    /// <summary>
    /// String set used (e.g., "Top 4", "Bottom 4", "All 6", "Inner 4")
    /// </summary>
    [BsonElement("stringSet")]
    public string? StringSet { get; set; }

    /// <summary>
    /// CAGED shape if applicable (C, A, G, E, D, or hybrid)
    /// </summary>
    [BsonElement("cagedShape")]
    public string? CagedShape { get; set; }

    /// <summary>
    /// Shell voicing family if applicable
    /// </summary>
    [BsonElement("shellFamily")]
    public string? ShellFamily { get; set; }

    // ========== CONTEXTUAL & MUSICAL HOOKS ==========

    /// <summary>
    /// Common chord substitutions (e.g., "tritone sub for G7", "relative minor of F")
    /// </summary>
    [BsonElement("commonSubstitutions")]
    public string[]? CommonSubstitutions { get; set; }

    /// <summary>
    /// Suggested play styles (strum, arpeggiate, hybrid-pick, fingerstyle, palm-mute)
    /// </summary>
    [BsonElement("playStyles")]
    public string[]? PlayStyles { get; set; }

    /// <summary>
    /// Genre associations (jazz, blues, rock, folk, neo-soul, classical, flamenco)
    /// </summary>
    [BsonElement("genreTags")]
    public string[]? GenreTags { get; set; }

    /// <summary>
    /// Famous song/riff references (e.g., "Hendrix Purple Haze", "Steely Dan Peg")
    /// </summary>
    [BsonElement("songReferences")]
    public string[]? SongReferences { get; set; }

    // ========== METADATA & AI ==========

    /// <summary>
    /// List of semantic tags for filtering (e.g., "jazz", "rootless", "beginner-friendly")
    /// </summary>
    [BsonElement("semanticTags")]
    public string[] SemanticTags { get; set; } = [];

    /// <summary>
    /// Vector embedding for semantic search
    /// </summary>
    [BsonElement("embedding")]
    public float[]? Embedding { get; set; }

    /// <summary>
    /// The model used to generate the embedding
    /// </summary>
    [BsonElement("embeddingModel")]
    public string? EmbeddingModel { get; set; }

    /// <summary>
    /// Full structured analysis in YAML/JSON for detailed retrieval by chatbots
    /// </summary>
    [BsonElement("fullAnalysis")]
    public string? FullAnalysis { get; set; }

    /// <summary>
    /// Timestamp of last analysis/update
    /// </summary>
    [BsonElement("lastUpdated")]
    public DateTime? LastUpdated { get; set; }
}
