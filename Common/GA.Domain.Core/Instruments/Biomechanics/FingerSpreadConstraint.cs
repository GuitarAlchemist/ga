namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Biomechanical model of human hand for guitar playing
/// </summary>
/// <remarks>
///     Based on average adult hand dimensions and joint constraints.
///     Can be customized for individual hand sizes.
///     References:
///     - "Hand Anthropometry" - NASA STD-3000
///     - "Biomechanics of the Hand" - Tubiana et al.
/// </remarks>
public record FingerSpreadConstraint
{
    public required FingerType Primary { get; init; }
    public required FingerType Secondary { get; init; }
    public float PreferredSeparationMm { get; init; }
    public float MaxSeparationMm { get; init; }
    public float MinSeparationMm { get; init; }
}
