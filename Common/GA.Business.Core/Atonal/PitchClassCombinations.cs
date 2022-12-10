namespace GA.Business.Core.Atonal;

using GA.Core.Combinatorics;

/// <summary>
/// Computes all possible combinations of pitch classes.
/// </summary>
[PublicAPI]
public class PitchClassCombinations : Combinations<PitchClass>
{
    public static readonly PitchClassCombinations SharedInstance = new();
}