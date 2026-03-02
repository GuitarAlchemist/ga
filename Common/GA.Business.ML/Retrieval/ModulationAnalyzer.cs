namespace GA.Business.ML.Retrieval;

using Domain.Core.Theory.Atonal;

/// <summary>
///     Analyzes progression drift on the Phase Sphere to identify potential modulation targets.
/// </summary>
public class ModulationAnalyzer(MusicalEmbeddingGenerator generator)
{
    /// <summary>
    ///     Identifies likely keys the progression is modulating toward.
    /// </summary>
    public List<ModulationTarget> IdentifyTargets(List<ChordVoicingRagDocument> progression)
    {
        if (progression.Count < 3)
        {
            return [];
        }

        // 1. Calculate the current barycenter (Current harmonic center)
        var barycenter = CalculateBarycenter(progression);

        // 2. Identify the "Key Centers" on the Phase Sphere
        // In Spectral domain (Partition 5), Major keys are distributed around the circle.
        // We compare the barycenter to each of the 12 possible major keys.

        var targets = new List<ModulationTarget>();
        for (var i = 0; i < 12; i++)
        {
            var keyPc = PitchClass.FromValue(i);
            float[] keyEmbedding = GetKeyPrototype(i);

            // Calculate distance on the Phase Sphere (Geodesic similarity)
            var distance = SpectralRetrievalService.CalculateWeightedSimilarity(
                barycenter, keyEmbedding, SpectralRetrievalService.SearchPreset.Spectral);

            targets.Add(new(keyPc, distance, 1.0 - distance));
        }

        return
        [
            .. targets
                .OrderByDescending(t => t.Confidence)
                .Take(3)
        ];
    }

    private float[] CalculateBarycenter(List<ChordVoicingRagDocument> progression)
    {
        var dim = EmbeddingSchema.TotalDimension;
        var barycenter = new float[dim];
        var validDocs = progression.Where(d => d.Embedding != null).ToList();
        if (validDocs.Count == 0)
        {
            return barycenter;
        }

        // 1. Average non-periodic partitions linearly
        foreach (var doc in validDocs)
        {
            for (var i = 0; i < EmbeddingSchema.SpectralOffset; i++)
            {
                barycenter[i] += doc.Embedding![i];
            }
        }

        for (var i = 0; i < EmbeddingSchema.SpectralOffset; i++)
        {
            barycenter[i] /= validDocs.Count;
        }

        // 2. Average SPECTRAL partition (Phase-Aware)
        // Indices 96-101: Mags, 102-107: Phases
        for (var k = 0; k < 6; k++)
        {
            double sumReal = 0;
            double sumImag = 0;
            foreach (var doc in validDocs)
            {
                var mag = doc.Embedding![EmbeddingSchema.FourierMagK1 + k];
                var phase = doc.Embedding![EmbeddingSchema.FourierPhaseK1 + k] * 2.0 * Math.PI - Math.PI;
                sumReal += mag * Math.Cos(phase);
                sumImag += mag * Math.Sin(phase);
            }

            var avgReal = sumReal / validDocs.Count;
            var avgImag = sumImag / validDocs.Count;
            var avgMag = Math.Sqrt(avgReal * avgReal + avgImag * avgImag);
            var avgPhase = (Math.Atan2(avgImag, avgReal) + Math.PI) / (2.0 * Math.PI);

            barycenter[EmbeddingSchema.FourierMagK1 + k] = (float)avgMag;
            barycenter[EmbeddingSchema.FourierPhaseK1 + k] = (float)avgPhase;
        }

        return barycenter;
    }

    private float[] GetKeyPrototype(int root)
    {
        // Use a full Major Scale for the key prototype to provide a complete "Diatonic Signature".
        // Root, M2, M3, P4, P5, M6, M7
        var pcs = new[]
        {
            root,
            (root + 2) % 12,
            (root + 4) % 12,
            (root + 5) % 12,
            (root + 7) % 12,
            (root + 9) % 12,
            (root + 11) % 12
        };
        var midi = pcs.Select(p => 60 + p).ToArray();

        var pcsList = pcs.Select(p => PitchClass.FromValue(p)).ToList();
        var pcsSet = new PitchClassSet(pcsList);

        var doc = new ChordVoicingRagDocument
        {
            Id = $"key-{root}", ChordName = "Prototype",
            MidiNotes = midi,
            PitchClasses = pcs,
            PossibleKeys = [],
            YamlAnalysis = "{}",
            PitchClassSet = string.Join(",", pcs),
            IntervalClassVector = pcsSet.IntervalClassVector.ToString(),
            AnalysisEngine = "System",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "0",
            Diagram = "",
            SemanticTags = ["prototype"],
            SearchableText = "",
            Consonance = 1.0 // Prototypes are perfectly consonant
        };

        return generator.GenerateEmbeddingAsync(doc).GetAwaiter().GetResult();
    }

    public record ModulationTarget(PitchClass Key, double Confidence, double DistanceToCurrent);
}
