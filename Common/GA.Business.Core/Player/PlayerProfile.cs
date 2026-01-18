namespace GA.Business.Core.Player;

using System;
using GA.Business.Core.Fretboard.Biomechanics;

/// <summary>
/// Defines the physical and stylistic characteristics of a player.
/// Used to customize ergonomic costs and generative realization.
/// </summary>
public record PlayerProfile
{
    public string Name { get; init; } = "Default Player";
    
    // --- Physical Constraints ---
    public HandSize HandSize { get; init; } = HandSize.Medium;
    
    /// <summary>Maximum comfortable physical stretch in millimeters.</summary>
    public double MaxComfortableStretchMm { get; init; } = BiomechanicsConstants.ComfortableHandSpanMm * 0.6; // approx 130mm

    // --- Difficulty Weights (Multipliers) ---
    public double StretchWeight { get; init; } = 100.0;
    public double SkipWeight { get; init; } = 1.5;
    public double RegisterWeight { get; init; } = 0.5;
    public double TensionWeight { get; init; } = 0.1;
    public double FatFingerWeight { get; init; } = 1.0;

    // --- Preferences ---
    public int PreferredMinFret { get; init; } = 3;
    public int PreferredMaxFret { get; init; } = 9;
    
    /// <summary>
    /// Creates a profile optimized for beginners.
    /// </summary>
    public static PlayerProfile Beginner() => new()
    {
        Name = "Beginner",
        StretchWeight = 200.0, // Harder penalties for stretches
        SkipWeight = 3.0,      // Strongly avoid string skips
        PreferredMaxFret = 5   // Stick to open position
    };

    /// <summary>
    /// Creates a profile optimized for jazz (favors mid-neck and complex voicings).
    /// </summary>
    public static PlayerProfile Jazz() => new()
    {
        Name = "Jazz",
        PreferredMinFret = 5,
        PreferredMaxFret = 12,
        SkipWeight = 0.5 // Jazzers are okay with string skips (Shells)
    };
}
