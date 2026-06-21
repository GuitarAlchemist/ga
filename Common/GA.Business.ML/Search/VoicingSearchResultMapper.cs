namespace GA.Business.ML.Search;

using System.Linq;
using GA.Business.ML.Rag.Models;
using GA.Domain.Services.Fretboard.Voicings.Core;

/// <summary>
/// Single projection of a <see cref="VoicingEmbedding"/> onto a
/// <see cref="ChordVoicingRagDocument"/> / <see cref="VoicingSearchResult"/>.
/// </summary>
/// <remarks>
/// Candidate #6 from /improve-codebase-architecture. <c>MapToSearchResult</c> was
/// duplicated in <see cref="CpuVoicingSearchStrategy"/> and
/// <see cref="GpuVoicingSearchStrategy"/> with three silently-diverging fields
/// (DifficultyScore, Roughness, CagedShape) — the exact bug class
/// <c>VoicingFilterEngine</c> was extracted to prevent. Here every field is
/// populated once; the only per-strategy input is the engine label.
/// OPTK keeps its own thinner mapper by design (ADR-0002: index-bound field set).
/// </remarks>
internal static class VoicingSearchResultMapper
{
    /// <summary>Projects a scored <paramref name="voicing"/> into a search result.</summary>
    public static VoicingSearchResult FromVoicingEmbedding(
        VoicingEmbedding voicing, double score, string query, string analysisEngine)
    {
        var document = new ChordVoicingRagDocument
        {
            Id = voicing.Id,
            SearchableText = voicing.Description,
            ChordName = voicing.ChordName,
            VoicingType = voicing.VoicingType,
            Position = voicing.Position,
            Difficulty = voicing.Difficulty,
            ModeName = voicing.ModeName,
            ModalFamily = voicing.ModalFamily,
            SemanticTags = voicing.SemanticTags,
            PossibleKeys = voicing.PossibleKeys,
            PrimeFormId = voicing.PrimeFormId,
            TranslationOffset = voicing.TranslationOffset,
            YamlAnalysis = voicing.Description,
            Diagram = voicing.Diagram,
            MidiNotes = voicing.MidiNotes,
            PitchClasses = [.. voicing.MidiNotes.Select(n => n % 12).Distinct().OrderBy(p => p)],
            PitchClassSet = voicing.PitchClassSet,
            IntervalClassVector = voicing.IntervalClassVector,
            MinFret = voicing.MinFret,
            MaxFret = voicing.MaxFret,
            BarreRequired = voicing.BarreRequired,
            HandStretch = voicing.HandStretch,

            AnalysisEngine = analysisEngine,
            AnalysisVersion = "1.0.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = voicing.PrimeFormId,
            StackingType = voicing.StackingType,
            RootPitchClass = voicing.RootPitchClass,
            MidiBassNote = voicing.MidiBassNote,
            DifficultyScore = voicing.Difficulty == "Beginner" ? 1.0 : voicing.Difficulty == "Intermediate" ? 2.0 : 3.0,

            HarmonicFunction = voicing.HarmonicFunction,
            IsNaturallyOccurring = voicing.IsNaturallyOccurring,
            HasGuideTones = voicing.HasGuideTones,
            IsRootless = voicing.IsRootless,
            Inversion = voicing.Inversion,
            TopPitchClass = voicing.TopPitchClass,
            Consonance = voicing.ConsonanceScore,
            Brightness = voicing.BrightnessScore,
            Roughness = 1.0 - voicing.ConsonanceScore,
            OmittedTones = voicing.OmittedTones,

            // AI Agent Metadata
            TexturalDescription = voicing.TexturalDescription,
            DoubledTones = voicing.DoubledTones,
            AlternateNames = voicing.AlternateNames,
            CagedShape = voicing.CagedShape
        };

        return new VoicingSearchResult(document, score, query);
    }
}
