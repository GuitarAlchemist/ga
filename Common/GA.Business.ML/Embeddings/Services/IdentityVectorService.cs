namespace GA.Business.ML.Embeddings.Services;

/// <summary>
/// Generates the IDENTITY partition of the musical embedding.
/// Corresponds to dimensions 0-5 of the standard musical vector.
/// Implements OPTIC-K Schema v1.3.1 (Indices 0-5 unchanged since v1.1).
/// </summary>
public class IdentityVectorService
{
    public const int Dimension = 6;

    public enum ObjectKind
    {
        Unknown = -1,
        Chord = 0,
        Scale = 1,
        Voicing = 2,
        Shape = 3,
        IntervalSet = 4,
        PitchClassSet = 5
    }

    public double[] ComputeEmbedding(ObjectKind kind)
    {
        var v = new double[Dimension];

        if (kind != ObjectKind.Unknown)
        {
            // Primary type one-hot
            v[(int)kind] = 1.0;

            // Composite Logic (Soft One-Hot cascade)
            // A Voicing is also a Chord
            if (kind == ObjectKind.Voicing) v[(int)ObjectKind.Chord] = 1.0;

            // Chords and Scales and Voicings all imply a PitchClassSet
            if (kind == ObjectKind.Chord || kind == ObjectKind.Scale || kind == ObjectKind.Voicing)
                v[(int)ObjectKind.PitchClassSet] = 1.0;
        }

        return v;
    }
}
