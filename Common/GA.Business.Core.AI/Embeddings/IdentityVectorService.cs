namespace GA.Business.Core.AI.Embeddings;

/// <summary>
/// Generates embeddings for Identity (v1.1).
/// Defines what kind of musical entity this vector represents.
/// Corresponds to dimensions 0-5 of the standard musical vector.
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
