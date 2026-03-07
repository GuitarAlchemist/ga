namespace GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Options for OPTIC-style voice-leading computations (<see href="https://en.wikipedia.org/wiki/Voice_leading" />).
/// </summary>
[PublicAPI]
public sealed class VoiceLeadingOptions
{
    public int? Voices { get; init; }
    public bool OctaveEquivalence { get; init; } = true;
    public bool PermutationEquivalence { get; init; } = true;
    public bool TranspositionEquivalence { get; init; } = true; // default to OPT rather than just OP
    public bool InversionEquivalence { get; init; } = false; // configurable
    public double[]? VoiceWeights { get; init; }

    public static VoiceLeadingOptions Default { get; } = new();
}