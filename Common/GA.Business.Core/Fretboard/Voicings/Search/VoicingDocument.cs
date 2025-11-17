namespace GA.Business.Core.Fretboard.Voicings.Search;

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
    /// Semantic tags for filtering and categorization
    /// </summary>
    public required string[] SemanticTags { get; init; }

    /// <summary>
    /// Prime form ID for equivalence class identification
    /// </summary>
    public string? PrimeFormId { get; init; }

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
    public bool BarreRequired { get; init; }

    /// <summary>
    /// Creates a VoicingDocument from a musical analysis
    /// </summary>
    public static VoicingDocument FromAnalysis(
        Voicing voicing,
        MusicalVoicingAnalysis analysis,
        string? primeFormId = null,
        int translationOffset = 0)
    {
        var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
        var id = $"voicing_{diagram.Replace("-", "_").Replace("x", "m")}";

        // Build searchable text
        var searchableText = BuildSearchableText(analysis, diagram);

        // Build YAML analysis
        var yamlAnalysis = BuildYamlAnalysis(voicing, analysis);

        return new VoicingDocument
        {
            Id = id,
            SearchableText = searchableText,
            ChordName = analysis.ChordId.ChordName,
            VoicingType = analysis.VoicingCharacteristics.DropVoicing,
            Position = analysis.PhysicalLayout.HandPosition,
            Difficulty = analysis.PlayabilityInfo.Difficulty,
            ModeName = analysis.ModeInfo?.ModeName,
            ModalFamily = analysis.ModeInfo?.FamilyName,
            SemanticTags = [.. analysis.SemanticTags],
            PrimeFormId = primeFormId ?? analysis.EquivalenceInfo?.PrimeFormId,
            TranslationOffset = translationOffset != 0 ? translationOffset : (analysis.EquivalenceInfo?.TranslationOffset ?? 0),
            YamlAnalysis = yamlAnalysis,
            Diagram = diagram,
            MidiNotes = [.. analysis.MidiNotes.Select(n => n.Value)],
            PitchClassSet = analysis.PitchClassSet.ToString(),
            IntervalClassVector = analysis.IntervallicInfo.IntervalClassVector,
            MinFret = analysis.PhysicalLayout.MinFret,
            MaxFret = analysis.PhysicalLayout.MaxFret,
            HandStretch = analysis.PlayabilityInfo.HandStretch,
            BarreRequired = analysis.PlayabilityInfo.BarreRequired
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

        return sb.ToString();
    }
}

