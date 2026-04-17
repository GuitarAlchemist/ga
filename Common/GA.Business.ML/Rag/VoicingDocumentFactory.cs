namespace GA.Business.ML.Rag;

using Core.Analysis.Voicings;
using Domain.Core.Instruments.Fretboard.Voicings.Core;
using Domain.Core.Theory.Atonal;
using Models;

public static class VoicingDocumentFactory
{
    public static ChordVoicingRagDocument FromAnalysis(
        Voicing voicing,
        MusicalVoicingAnalysis analysis,
        string tuningId = "Standard",
        int capo = 0,
        string? primeFormId = null,
        int translationOffset = 0)
    {
        var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
        var distinctId = $"{tuningId.ToLower()}_{capo}_{diagram.Replace("-", "_").Replace("x", "m")}";
        var id = $"voicing_{distinctId}";

        return new()
        {
            Id = id,
            SearchableText = BuildSearchableText(analysis, diagram),
            Diagram = diagram,
            YamlAnalysis = BuildYamlAnalysis(voicing, analysis),
            AnalysisEngine = "GuitarAlchemist.VoicingAnalyzer",
            AnalysisVersion = "1.0.0",
            Jobs = [],
            TuningId = tuningId,
            PitchClassSetId = primeFormId ?? analysis.EquivalenceInfo?.PrimeFormId ?? "Unknown",
            // Phase C: prefer CanonicalName (register-invariant) over legacy ChordName
            // (which may include a voicing-specific "/bass" suffix that leaks into SYMBOLIC
            // embedding dims and destroys cross-instrument consistency).
            ChordName = analysis.ChordId.CanonicalName ?? analysis.ChordId.ChordName,
            RootPitchClass = analysis.MidiNotes.Length > 0 ? analysis.MidiNotes[0] % 12 : 0,
            MidiBassNote = analysis.MidiNotes.Length > 0 ? analysis.MidiNotes[0] : 0,
            VoicingType = analysis.VoicingCharacteristics.DropVoicing,
            IsRootless = analysis.VoicingCharacteristics.IsRootless,
            HasGuideTones = analysis.ToneInventory.HasGuideTones,
            OmittedTones = [.. analysis.ToneInventory.OmittedTones],
            Inversion = CalculateInversion(analysis.MidiNotes.Length > 0 ? analysis.MidiNotes[0] : 0,
                analysis.ChordId.RootPitchClass != null ? PitchClass.Parse(analysis.ChordId.RootPitchClass, null).Value : 0),
            Brightness = analysis.PerceptualQualities.Brightness,
            Consonance = analysis.PerceptualQualities.ConsonanceScore,
            Roughness = analysis.PerceptualQualities.Roughness,
            TexturalDescription = analysis.PerceptualQualities.TexturalDescription,
            DoubledTones = [.. analysis.ToneInventory.DoubledTones],
            AlternateNames = analysis.AlternateChordNames != null ? [.. analysis.AlternateChordNames] : [],
            Position = analysis.PhysicalLayout.HandPosition,
            MinFret = analysis.PhysicalLayout.MinFret,
            MaxFret = analysis.PhysicalLayout.MaxFret,
            HandStretch = analysis.PlayabilityInfo.HandStretch,
            BarreRequired = analysis.PlayabilityInfo.BarreRequired,
            Difficulty = analysis.PlayabilityInfo.Difficulty,
            DifficultyScore = analysis.PlayabilityInfo.DifficultyScore,
            HarmonicFunction = analysis.ChordId.HarmonicFunction,
            IsNaturallyOccurring = analysis.ChordId.IsNaturallyOccurring,
            ModeName = analysis.ModeInfo?.ModeName,
            ModalFamily = analysis.ModeInfo?.FamilyName,
            PossibleKeys = [.. analysis.PitchClassSet.GetCompatibleKeys().Select(k => k.ToString())],
            SemanticTags = [.. analysis.SemanticTags],
            PrimeFormId = primeFormId ?? analysis.EquivalenceInfo?.PrimeFormId,
            ForteCode = analysis.EquivalenceInfo?.ForteCode ??
                        (ForteCatalog.TryGetForteNumber(analysis.PitchClassSet.PrimeForm, out var forte)
                            ? forte.ToString()
                            : null),
            TranslationOffset = translationOffset != 0
                ? translationOffset
                : analysis.EquivalenceInfo?.TranslationOffset ?? 0,
            MidiNotes = [.. analysis.MidiNotes],
            PitchClasses = [.. analysis.PitchClassSet.Select(p => p.Value)],
            PitchClassSet = analysis.PitchClassSet.ToString(),
            IntervalClassVector = analysis.IntervallicInfo.IntervalClassVector
        };
    }

    private static int CalculateInversion(int midiBass, int rootPc)
    {
        var bassPc = midiBass % 12;
        var interval = (bassPc - rootPc + 12) % 12;
        return interval switch
        {
            0 => 0, 3 or 4 => 1, 6 or 7 => 2, 10 or 11 => 3, _ => -1
        };
    }

    private static string BuildSearchableText(MusicalVoicingAnalysis analysis, string diagram)
    {
        var sb = new StringBuilder();
        // Phase C: prefer CanonicalName so the text embedding stays register-invariant.
        var displayChordName = analysis.ChordId.CanonicalName ?? analysis.ChordId.ChordName;
        if (displayChordName != null)
        {
            sb.Append($"{displayChordName} ");
        }

        if (analysis.VoicingCharacteristics.DropVoicing != null)
        {
            sb.Append($"{analysis.VoicingCharacteristics.DropVoicing} voicing ");
        }

        sb.Append(analysis.VoicingCharacteristics.IsOpenVoicing ? "open voicing " : "closed voicing ");
        if (analysis.VoicingCharacteristics.IsRootless)
        {
            sb.Append("rootless ");
        }

        sb.Append($"{analysis.PhysicalLayout.HandPosition} {analysis.PlayabilityInfo.Difficulty} difficulty ");
        if (analysis.ModeInfo != null)
        {
            sb.Append($"{analysis.ModeInfo.ModeName} mode {analysis.ModeInfo.FamilyName} ");
        }

        if (analysis.ChordId.FunctionalDescription != null)
        {
            sb.Append($"{analysis.ChordId.FunctionalDescription} ");
        }

        sb.Append(string.Join(" ", analysis.SemanticTags));
        sb.Append($" diagram:{diagram}");
        return sb.ToString();
    }

    private static string BuildYamlAnalysis(Voicing voicing, MusicalVoicingAnalysis analysis) =>
        $"diagram: \"{voicing}\"\nmidi_notes: [{string.Join(", ", analysis.MidiNotes)}]"; // Stubbed for now
}
