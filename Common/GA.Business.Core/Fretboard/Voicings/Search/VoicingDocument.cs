namespace GA.Business.Core.Fretboard.Voicings.Search;

using System.Linq;
using System.Text;
using Analysis;
using Core;

/// <summary>
/// Document representation of a voicing for vector store indexing
/// </summary>
public record VoicingDocument
{
    /// <summary>
    /// Unique identifier for the voicing
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Searchable text representation for embedding generation
    /// </summary>
    public required string SearchableText { get; init; }

    /// <summary>
    /// Chord name (e.g., "C Major", "Dm7")
    /// </summary>
    public string? ChordName { get; init; }

    /// <summary>
    /// Voicing type (e.g., "Drop-2", "Rootless")
    /// </summary>
    public string? VoicingType { get; init; }

    /// <summary>
    /// Hand position on fretboard (e.g., "Open Position", "Middle Position")
    /// </summary>
    public string? Position { get; init; }

    /// <summary>
    /// Difficulty level (Beginner, Intermediate, Advanced)
    /// </summary>
    public string? Difficulty { get; init; }

    /// <summary>
    /// Mode name if applicable (e.g., "Dorian", "Phrygian Dominant")
    /// </summary>
    public string? ModeName { get; init; }

    /// <summary>
    /// Modal family if applicable (e.g., "Major Scale Family", "Harmonic Minor Family")
    /// </summary>
    public string? ModalFamily { get; init; }

    /// <summary>
    /// Keys compatible with this voicing (e.g. "C Major", "A Minor")
    /// </summary>
    public required string[] PossibleKeys { get; init; }

    /// <summary>
    /// CAGED system shape (C, A, G, E, D, or hybrid)
    /// </summary>
    public string? CagedShape { get; init; }

    /// <summary>
    /// Semantic tags for filtering and categorization
    /// </summary>
    public required string[] SemanticTags { get; init; }

    /// <summary>
    /// Prime form ID for equivalence class identification
    /// </summary>
    public string? PrimeFormId { get; init; }

    /// <summary>
    /// Forte Number (e.g. "3-11") for set-theory analysis.
    /// </summary>
    public string? ForteCode { get; init; }

    /// <summary>
    /// Translation offset from prime form
    /// </summary>
    public int TranslationOffset { get; init; }

    /// <summary>
    /// Complete YAML analysis for detailed retrieval
    /// </summary>
    public required string YamlAnalysis { get; init; }

    /// <summary>
    /// Fretboard diagram (e.g., "x-3-2-0-1-0")
    /// </summary>
    public required string Diagram { get; init; }

    /// <summary>
    /// MIDI notes array
    /// </summary>
    public required int[] MidiNotes { get; init; }

    /// <summary>
    /// Pitch classes array (0-11)
    /// </summary>
    public required int[] PitchClasses { get; init; }

    /// <summary>
    /// Pitch class set representation
    /// </summary>
    public required string PitchClassSet { get; init; }

    /// <summary>
    /// Interval class vector
    /// </summary>
    public required string IntervalClassVector { get; init; }

    /// <summary>
    /// Minimum fret position
    /// </summary>
    public int MinFret { get; init; }

    /// <summary>
    /// Maximum fret position
    /// </summary>
    public int MaxFret { get; init; }

    /// <summary>
    /// Hand stretch (fret span)
    /// </summary>
    public int HandStretch { get; init; }

    /// <summary>
    /// Whether a barre is required
    /// </summary>
    /// <summary>
    /// Whether a barre is required
    /// </summary>
    public bool BarreRequired { get; init; }

    // --- Metadata ---
    public required string AnalysisEngine { get; init; }
    public required string AnalysisVersion { get; init; }
    public required string[] Jobs { get; init; }

    // --- Identifiers ---
    public required string TuningId { get; init; }
    public required string PitchClassSetId { get; init; }
    
    // --- Musical Identity ---
    // --- Musical Identity ---
    public int? RootPitchClass { get; init; }
    public int MidiBassNote { get; init; }
    public string? StackingType { get; init; }
    public string? HarmonicFunction { get; init; } // Phase 3
    public bool IsNaturallyOccurring { get; init; } // Phase 3
    public bool IsRootless { get; init; } // Phase 3
    public bool HasGuideTones { get; init; } // Phase 3
    public int Inversion { get; init; } // Phase 3
    public string[]? OmittedTones { get; init; } // Phase 3

    // --- Perceptual & Playability Metrics ---
    public double Brightness { get; init; }
    public double Consonance { get; init; }
    public double Roughness { get; init; }
    public int? TopPitchClass { get; init; } // Added for Chord Melody support
    public string? TexturalDescription { get; init; } // Added for AI Agents "warm", "muddy"
    public string[]? DoubledTones { get; init; } // Added for AI Agents "doubled 5th"
    public string[]? AlternateNames { get; init; } // Added for AI Agents "C6"
    public double DifficultyScore { get; init; }
    
    // --- Embeddings (Phase 7 Dual Strategy) ---
    public double[]? Embedding { get; init; }      // 78-dim OPTIC-K
    public double[]? TextEmbedding { get; init; }  // 384-dim BERT/ONNX

    /// <summary>
    /// Creates a VoicingDocument from a musical analysis
    /// </summary>
    public static VoicingDocument FromAnalysis(
        Voicing voicing,
        MusicalVoicingAnalysis analysis,
        string tuningId = "Standard",
        int capo = 0,
        string? primeFormId = null,
        int translationOffset = 0)
    {
        var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
        // ID: v1_{tuning}_{capo}_{diagram}
        var distinctId = $"{tuningId.ToLower()}_{capo}_{diagram.Replace("-", "_").Replace("x", "m")}";
        var id = $"voicing_{distinctId}";

        // Build searchable text
        var searchableText = BuildSearchableText(analysis, diagram);

        // Build YAML analysis
        var yamlAnalysis = BuildYamlAnalysis(voicing, analysis);

        return new()
        {
            Id = id,
            // Metadata
            AnalysisEngine = "GuitarAlchemist.VoicingAnalyzer",
            AnalysisVersion = "1.0.0",
            Jobs = [],

            // Identifiers
            TuningId = tuningId,
            PitchClassSetId = primeFormId ?? analysis.EquivalenceInfo?.PrimeFormId ?? "Unknown",
            
            // Text & Diagram
            SearchableText = searchableText,
            Diagram = diagram,
            YamlAnalysis = yamlAnalysis,

            // Musical Identity
            ChordName = analysis.ChordId.ChordName,
            RootPitchClass = analysis.MidiNotes.Length > 0 ? analysis.MidiNotes[0].Value % 12 : 0, // Approx
            MidiBassNote = analysis.MidiNotes.Length > 0 ? analysis.MidiNotes[0].Value : 0,

            // Voicing Features
            VoicingType = analysis.VoicingCharacteristics.DropVoicing,
            StackingType = analysis.SemanticTags.Contains("quartal") ? "Quartal" : "Tertian", // Simple inference
            IsRootless = analysis.VoicingCharacteristics.IsRootless,
            HasGuideTones = analysis.ToneInventory.HasGuideTones,
            OmittedTones = [.. analysis.ToneInventory.OmittedTones],
            
            // Inversion Logic
            Inversion = CalculateInversion(
                analysis.MidiNotes.Length > 0 ? analysis.MidiNotes[0].Value : 0, 
                analysis.ChordId.RootPitchClass?.Value ?? 0),

            // Perceptual
            Brightness = analysis.PerceptualQualities.Brightness, 
            Consonance = analysis.PerceptualQualities.ConsonanceScore,
            Roughness = analysis.PerceptualQualities.Roughness,
            TexturalDescription = analysis.PerceptualQualities.TexturalDescription,
            
            // Structural Features
            DoubledTones = [.. analysis.ToneInventory.DoubledTones],
            AlternateNames = analysis.AlternateChordNames != null ? [.. analysis.AlternateChordNames] : [],
            
            // Physical
            Position = analysis.PhysicalLayout.HandPosition,
            MinFret = analysis.PhysicalLayout.MinFret,
            MaxFret = analysis.PhysicalLayout.MaxFret,
            HandStretch = analysis.PlayabilityInfo.HandStretch,
            BarreRequired = analysis.PlayabilityInfo.BarreRequired,
            Difficulty = analysis.PlayabilityInfo.Difficulty,
            DifficultyScore = analysis.PlayabilityInfo.DifficultyScore,

            // Musical Function
            HarmonicFunction = analysis.ChordId.HarmonicFunction,
            IsNaturallyOccurring = analysis.ChordId.IsNaturallyOccurring,

            // Context
            ModeName = analysis.ModeInfo?.ModeName,
            ModalFamily = analysis.ModeInfo?.FamilyName,
            PossibleKeys = [.. analysis.PitchClassSet.GetCompatibleKeys().Select(k => k.ToString())],
            SemanticTags = [.. analysis.SemanticTags],
            PrimeFormId = primeFormId ?? analysis.EquivalenceInfo?.PrimeFormId,
            ForteCode = analysis.EquivalenceInfo?.ForteCode ?? (Atonal.ForteCatalog.TryGetForteNumber(analysis.PitchClassSet.PrimeForm, out var forte) ? forte.ToString() : null),
            TranslationOffset = translationOffset != 0 ? translationOffset : analysis.EquivalenceInfo?.TranslationOffset ?? 0,
            
            MidiNotes = [.. analysis.MidiNotes.Select(n => n.Value)],
            PitchClasses = [.. analysis.PitchClassSet.Select(p => p.Value)],
            PitchClassSet = analysis.PitchClassSet.ToString(),
            IntervalClassVector = analysis.IntervallicInfo.IntervalClassVector,
        };
    }

    private static int CalculateInversion(int midiBass, int rootPc)
    {
        var bassPc = midiBass % 12;
        var interval = (bassPc - rootPc + 12) % 12;
        return interval switch
        {
            0 => 0, // Root
            3 or 4 => 1, // 3rd (Minor or Major)
            6 or 7 => 2, // 5th (Dim or Perfect)
            10 or 11 => 3, // 7th (Minor or Major)
            _ => -1 // Other (e.g., 9th in bass)
        };
    }

    private static string BuildSearchableText(MusicalVoicingAnalysis analysis, string diagram)
    {
        var sb = new StringBuilder();

        // Chord name and type
        if (analysis.ChordId.ChordName != null)
        {
            sb.Append(analysis.ChordId.ChordName);
            sb.Append(' ');
        }

        // Voicing characteristics
        if (analysis.VoicingCharacteristics.DropVoicing != null)
        {
            sb.Append(analysis.VoicingCharacteristics.DropVoicing);
            sb.Append(" voicing ");
        }

        sb.Append(analysis.VoicingCharacteristics.IsOpenVoicing ? "open voicing " : "closed voicing ");

        if (analysis.VoicingCharacteristics.IsRootless)
        {
            sb.Append("rootless ");
        }

        // Position and difficulty
        sb.Append(analysis.PhysicalLayout.HandPosition);
        sb.Append(' ');
        sb.Append(analysis.PlayabilityInfo.Difficulty);
        sb.Append(" difficulty ");

        // Mode information
        if (analysis.ModeInfo != null)
        {
            sb.Append(analysis.ModeInfo.ModeName);
            sb.Append(" mode ");
            if (analysis.ModeInfo.FamilyName != null)
            {
                sb.Append(analysis.ModeInfo.FamilyName);
                sb.Append(' ');
            }
        }

        // Functional description
        if (analysis.ChordId.FunctionalDescription != null)
        {
            sb.Append(analysis.ChordId.FunctionalDescription);
            sb.Append(' ');
        }

        // Semantic tags
        sb.Append(string.Join(" ", analysis.SemanticTags));

        // Features
        if (analysis.VoicingCharacteristics.Features.Count > 0)
        {
            sb.Append(' ');
            sb.Append(string.Join(" ", analysis.VoicingCharacteristics.Features));
        }

        // Agentic Metadata (Texture, etc.)
        if (analysis.PerceptualQualities.TexturalDescription != null)
        {
            sb.Append(' ');
            sb.Append(analysis.PerceptualQualities.TexturalDescription);
            sb.Append(' ');
        }
        
        // Alternate names for semantic matching (e.g. "C6" matches "Am7")
        if (analysis.AlternateChordNames != null && analysis.AlternateChordNames.Count > 0)
        {
            sb.Append(' ');
            sb.Append(string.Join(" ", analysis.AlternateChordNames));
        }

        // Diagram for exact matching
        sb.Append(" diagram:");
        sb.Append(diagram);

        return sb.ToString();
    }

    private static string BuildYamlAnalysis(Voicing voicing, MusicalVoicingAnalysis analysis)
    {
        var sb = new StringBuilder();
        var diagram = voicing.ToString();
        var midiNotes = string.Join(", ", analysis.MidiNotes.Select(n => n.Value));
        var noteNames = string.Join(", ", analysis.MidiNotes.Select(n => $"\"{n}\""));
        var pitchClasses = analysis.PitchClassSet.ToString();

        sb.AppendLine($"diagram: \"{diagram}\"");
        sb.AppendLine($"midi_notes: [{midiNotes}]");
        sb.AppendLine($"notes: [{noteNames}]");
        sb.AppendLine($"pitch_classes: \"{pitchClasses}\"");
        sb.AppendLine("chord:");
        sb.AppendLine($"  name: \"{analysis.ChordId.ChordName ?? "Unknown"}\"");
        sb.AppendLine($"  key_function: \"{analysis.ChordId.FunctionalDescription ?? "Atonal"}\"");
        sb.AppendLine("voicing:");
        sb.AppendLine($"  type: \"{(analysis.VoicingCharacteristics.IsOpenVoicing ? "open" : "closed")}\"");
        if (analysis.VoicingCharacteristics.DropVoicing != null)
        {
            sb.AppendLine($"  drop_voicing: \"{analysis.VoicingCharacteristics.DropVoicing}\"");
        }
        if (analysis.ModeInfo != null)
        {
            sb.AppendLine("mode:");
            sb.AppendLine($"  name: \"{analysis.ModeInfo.ModeName}\"");
            if (analysis.ModeInfo.FamilyName != null)
            {
                sb.AppendLine($"  family: \"{analysis.ModeInfo.FamilyName}\"");
            }
        }
        sb.AppendLine("physical_layout:");
        sb.AppendLine($"  hand_position: \"{analysis.PhysicalLayout.HandPosition}\"");
        sb.AppendLine($"  fret_range: [{analysis.PhysicalLayout.MinFret}, {analysis.PhysicalLayout.MaxFret}]");
        sb.AppendLine("playability:");
        sb.AppendLine($"  difficulty: \"{analysis.PlayabilityInfo.Difficulty}\"");
        sb.AppendLine($"  hand_stretch: {analysis.PlayabilityInfo.HandStretch}");
        sb.AppendLine($"semantic_tags: [{string.Join(", ", analysis.SemanticTags.Select(t => $"\"{t}\""))}]");
        sb.AppendLine($"texture: \"{analysis.PerceptualQualities.TexturalDescription ?? "Neutral"}\"");
        sb.AppendLine($"doubled_tones: [{string.Join(", ", analysis.ToneInventory.DoubledTones.Select(t => $"\"{t}\""))}]");
        if (analysis.AlternateChordNames?.Count > 0)
        {
            sb.AppendLine($"alternate_names: [{string.Join(", ", analysis.AlternateChordNames.Select(n => $"\"{n}\""))}]");
        }

        return sb.ToString();
    }
}

