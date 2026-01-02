namespace GA.Business.Core.Fretboard.Shapes;

using Atonal.Grothendieck;

/// <summary>
///     Represents a transition between two fretboard shapes
/// </summary>
[PublicAPI]
public sealed record ShapeTransition
{
    /// <summary>
    ///     Source shape ID
    /// </summary>
    public required string FromId { get; init; }

    /// <summary>
    ///     Target shape ID
    /// </summary>
    public required string ToId { get; init; }

    /// <summary>
    ///     Grothendieck delta (harmonic change)
    /// </summary>
    public required GrothendieckDelta Delta { get; init; }

    /// <summary>
    ///     Harmonic cost (L1 norm of delta)
    /// </summary>
    public required double HarmonicCost { get; init; }

    /// <summary>
    ///     Physical cost (finger travel, position shift)
    /// </summary>
    public required double PhysicalCost { get; init; }

    /// <summary>
    ///     OPTIC voice-leading cost between voicings (Tymoczko space)
    /// </summary>
    public double VoiceLeadingCost { get; init; }

    /// <summary>
    ///     Weight applied to voice-leading cost when computing <see cref="ExtendedScore"/>.
    ///     Provided for transparency; 0.0 means VL did not influence the score.
    /// </summary>
    public double VoiceLeadingWeightUsed { get; init; }

    /// <summary>
    ///     Combined score (lower is better)
    /// </summary>
    public double Score => HarmonicCost + PhysicalCost;

    /// <summary>
    ///     Extended combined score including voice-leading if a non-zero weight was used.
    ///     Lower is better. Falls back to <see cref="Score"/> if <see cref="VoiceLeadingWeightUsed"/> is 0.
    /// </summary>
    public double ExtendedScore => Score + (VoiceLeadingWeightUsed > 0 ? VoiceLeadingWeightUsed * VoiceLeadingCost : 0);

    /// <summary>
    ///     Probability weight for Markov chain (higher is better)
    ///     Inverse of score, normalized
    /// </summary>
    public double Weight => 1.0 / (1.0 + Score);

    /// <summary>
    ///     Probability weight derived from <see cref="ExtendedScore"/> (when applicable).
    ///     Consumers preferring OPTIC-aware scoring can use this instead of <see cref="Weight"/>.
    /// </summary>
    public double ExtendedWeight => 1.0 / (1.0 + ExtendedScore);

    /// <summary>
    ///     Compute physical cost between two shapes
    /// </summary>
    public static double ComputePhysicalCost(FretboardShape from, FretboardShape to)
    {
        // Position shift cost
        var positionShift = Math.Abs(to.MinFret - from.MinFret);
        var shiftCost = positionShift * 0.5; // Each fret shift costs 0.5

        // Span change cost
        var spanChange = Math.Abs(to.Span - from.Span);
        var spanCost = spanChange * 0.3; // Span changes are moderately costly

        // String pattern change cost
        var stringPatternChange = ComputeStringPatternChange(from.StringMask, to.StringMask);
        var patternCost = stringPatternChange * 0.4;

        // Diagness change cost (box <-> diagonal transitions are harder)
        var diagnessChange = Math.Abs(to.Diagness - from.Diagness);
        var diagnessCost = diagnessChange * 0.6;

        return shiftCost + spanCost + patternCost + diagnessCost;
    }

    /// <summary>
    ///     Compute how different two string patterns are (0-1)
    /// </summary>
    private static double ComputeStringPatternChange(int mask1, int mask2)
    {
        var xor = mask1 ^ mask2;
        var bitCount = BitOperations.PopCount((uint)xor);
        return bitCount / 6.0; // Normalize by max strings (6)
    }

    public override string ToString()
    {
        return
            $"{FromId} â†’ {ToId}: {Delta.Explain()} (harm:{HarmonicCost:F2}, phys:{PhysicalCost:F2}, score:{Score:F2})";
    }
}
