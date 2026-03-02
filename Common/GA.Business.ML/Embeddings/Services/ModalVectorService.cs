namespace GA.Business.ML.Embeddings.Services;

using Domain.Core.Theory.Atonal;
using Rag.Models;
using Musical.Enrichment;

/// <summary>
///     Generates the MODAL partition of the musical embedding (Mode/Scale characteristic flavors).
///     Corresponds to dimensions 109-148 of the standard musical vector (v1.6).
///     Implements OPTIC-K Schema v1.6.
/// </summary>
public class ModalVectorService
{
    private readonly ModalCharacteristicIntervalService _intervalService = ModalCharacteristicIntervalService.Instance;

    /// <summary>
    ///     Computes the Modal partition of the embedding.
    /// </summary>
    public double[] ComputeEmbedding(ChordVoicingRagDocument doc)
    {
        var v = new double[EmbeddingSchema.ModalDim]; // Offset: 109, Dim: 40

        if (doc.PitchClasses.Length == 0)
        {
            return v;
        }

        var root = doc.RootPitchClass ?? 0;
        var pcs = doc.PitchClasses;

        // Convert voicing to Interval Set relative to Root (semitones, normalized to octave)
        var voicingIntervals = new HashSet<int>();
        foreach (var pc in pcs)
        {
            var interval = (pc - root + 12) % 12;
            voicingIntervals.Add(interval);
        }

        // === 1. Major Scale Modes (109-115) ===
        ComputeModeScore(v, EmbeddingSchema.ModalIonian - EmbeddingSchema.ModalOffset, "Ionian", voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalDorian - EmbeddingSchema.ModalOffset, "Dorian", voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalPhrygian - EmbeddingSchema.ModalOffset, "Phrygian", voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLydian - EmbeddingSchema.ModalOffset, "Lydian", voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalMixolydian - EmbeddingSchema.ModalOffset, "Mixolydian",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalAeolian - EmbeddingSchema.ModalOffset, "Aeolian", voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLocrian - EmbeddingSchema.ModalOffset, "Locrian", voicingIntervals);

        // === 2. Harmonic Minor Modes (116-122) ===
        ComputeModeScore(v, EmbeddingSchema.ModalHarmonicMinor - EmbeddingSchema.ModalOffset, "Harmonic minor",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLocrianNatural6 - EmbeddingSchema.ModalOffset, "Locrian ♮6",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalIonianAugmented - EmbeddingSchema.ModalOffset, "Ionian augmented",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalDorianSharp4 - EmbeddingSchema.ModalOffset, "Dorian ♯4",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalPhrygianDominant - EmbeddingSchema.ModalOffset, "Phrygian dominant",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLydianSharp2 - EmbeddingSchema.ModalOffset, "Lydian ♯2",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalAlteredDoubleFlat7 - EmbeddingSchema.ModalOffset, "Altered ♭♭7",
            voicingIntervals);

        // === 3. Melodic Minor Modes (123-129) ===
        ComputeModeScore(v, EmbeddingSchema.ModalMelodicMinor - EmbeddingSchema.ModalOffset, "Melodic minor",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalDorianFlat2 - EmbeddingSchema.ModalOffset, "Dorian ♭2",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLydianAugmented - EmbeddingSchema.ModalOffset, "Lydian ♯5",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLydianDominant - EmbeddingSchema.ModalOffset, "Lydian dominant",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalMixolydianFlat6 - EmbeddingSchema.ModalOffset, "Mixolydian ♭6",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLocrianNatural2 - EmbeddingSchema.ModalOffset, "Locrian ♮2",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalAltered - EmbeddingSchema.ModalOffset, "Altered", voicingIntervals);

        // === 4. Harmonic Major Modes (130-136) ===
        ComputeModeScore(v, EmbeddingSchema.ModalHarmonicMajor - EmbeddingSchema.ModalOffset, "Harmonic major",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalDorianFlat5 - EmbeddingSchema.ModalOffset, "Dorian b5",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalPhrygianFlat4 - EmbeddingSchema.ModalOffset, "Phrygian b4",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLydianFlat3 - EmbeddingSchema.ModalOffset, "Lydian b3",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalMixolydianFlat2 - EmbeddingSchema.ModalOffset, "Mixolydian b2",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLydianAugmentedSharp2 - EmbeddingSchema.ModalOffset,
            "Lydian augmented #2", voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalLocrianDoubleFlat7 - EmbeddingSchema.ModalOffset, "Locrian bb7",
            voicingIntervals);

        // === 5. Pentatonic and Other (137-141) ===
        ComputeModeScore(v, EmbeddingSchema.ModalPentatonicMajor - EmbeddingSchema.ModalOffset, "Major Pentatonic",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalPentatonicMinor - EmbeddingSchema.ModalOffset, "Minor Pentatonic",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalBlues - EmbeddingSchema.ModalOffset, "Blues", voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalWholeTone - EmbeddingSchema.ModalOffset, "Whole-tone",
            voicingIntervals);
        ComputeModeScore(v, EmbeddingSchema.ModalDiminished - EmbeddingSchema.ModalOffset, "Diminished",
            voicingIntervals);

        return v;
    }

    /// <summary>
    ///     Computes the Atonal Modal partition (v1.7) for any pitch class set.
    ///     Bridges the gap between tonal modes and set-theoretic modal families.
    /// </summary>
    public double[] ComputeAtonalModalEmbedding(ChordVoicingRagDocument doc)
    {
        var v = new double[EmbeddingSchema.AtonalModalDim];

        if (doc.PitchClasses.Length == 0)
        {
            return v;
        }

        var pcs = new PitchClassSet(doc.PitchClasses.Select(PitchClass.FromValue));

        // 1. Rank & Size (Normalization: 12 modes max in most families)
        v[EmbeddingSchema.AtonalModeRank - EmbeddingSchema.AtonalModalOffset] =
            Math.Clamp((double)pcs.ModeIndex / Math.Max(1, pcs.FamilySize - 1), 0, 1);
        v[EmbeddingSchema.AtonalFamilySize - EmbeddingSchema.AtonalModalOffset] =
            Math.Clamp(pcs.FamilySize / 12.0, 0, 1);

        // 2. Flags
        v[EmbeddingSchema.AtonalIsSymmetric - EmbeddingSchema.AtonalModalOffset] = pcs.IsMonomodal ? 1.0 : 0.0;
        v[EmbeddingSchema.AtonalIsZRelated - EmbeddingSchema.AtonalModalOffset] = pcs.IsZRelated ? 1.0 : 0.0;

        // 3. Hero Mode Check (Self-Correction)
        // If it's one of our 40 "Hero Modes", flag it
        var tonalVector = ComputeEmbedding(doc);
        v[EmbeddingSchema.AtonalIsHeroMode - EmbeddingSchema.AtonalModalOffset] =
            tonalVector.Any(val => val > 0.5) ? 1.0 : 0.0;

        // 4. ICV Profile (Normalized by cardinality)
        var icv = pcs.IntervalClassVector;
        var norm = (double)pcs.Count * (pcs.Count - 1) / 2.0; // Total possible intervals
        if (norm > 0)
        {
            for (var i = 1; i <= 6; i++)
            {
                v[EmbeddingSchema.AtonalIcvProfileOffset - EmbeddingSchema.AtonalModalOffset + (i - 1)] =
                    icv[IntervalClass.FromValue(i)] / norm;
            }
        }

        // 5. Advanced Atonal Metrics
        v[EmbeddingSchema.AtonalPrimeFormId - EmbeddingSchema.AtonalModalOffset] =
            pcs.PrimeForm != null ? pcs.PrimeForm.Id.Value / 4095.0 : 0;
        v[EmbeddingSchema.AtonalPcDiversity - EmbeddingSchema.AtonalModalOffset] = pcs.StepEntropy;
        v[EmbeddingSchema.AtonalStepBrightness - EmbeddingSchema.AtonalModalOffset] = pcs.StepBrightness;
        v[EmbeddingSchema.AtonalConsonancePotential - EmbeddingSchema.AtonalModalOffset] = pcs.ConsonancePotential;
        v[EmbeddingSchema.AtonalDissonanceIndex - EmbeddingSchema.AtonalModalOffset] = pcs.DissonanceIndex;
        v[EmbeddingSchema.AtonalCenterOfGravity - EmbeddingSchema.AtonalModalOffset] = pcs.CenterOfGravity;

        return v;
    }

    private void ComputeModeScore(double[] vector, int index, string modeName, HashSet<int> voicingIntervals)
    {
        if (index < 0 || index >= vector.Length)
        {
            return;
        }

        var characteristics = _intervalService.GetCharacteristicSemitones(modeName);
        var fullIntervals = _intervalService.GetModeIntervals(modeName);

        if (fullIntervals == null || fullIntervals.Count == 0)
        {
            return;
        }

        // Effective characteristics: if none defined, use full set
        var effectiveCharacteristics = characteristics != null && characteristics.Count > 0
            ? characteristics
            : fullIntervals;

        // 1. Matches
        var matchCount = effectiveCharacteristics.Count(s => voicingIntervals.Contains(s));

        // 2. Conflicts
        var conflictCount = voicingIntervals.Count(interval => !fullIntervals.Contains(interval));

        // 3. Saturation (Full mode coverage)
        var saturation = (double)fullIntervals.Intersect(voicingIntervals).Count() / fullIntervals.Count;

        if (conflictCount > 0)
        {
            vector[index] = 0.0;
            return;
        }

        // Score based on how many characteristics are present.
        var characteristicRatio = (double)matchCount / effectiveCharacteristics.Count;

        // Boost if fully saturated
        if (saturation >= 0.99)
        {
            characteristicRatio = 1.0;
        }

        vector[index] = Math.Clamp(characteristicRatio, 0.0, 1.0);
    }
}
