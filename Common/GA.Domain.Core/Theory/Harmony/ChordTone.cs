namespace GA.Domain.Core.Theory.Harmony;

using Atonal;

/// <summary>
///     Represents a chord tone with its harmonic function
///     (<see href="https://en.wikipedia.org/wiki/Chord_tone" />)
/// </summary>
/// <param name="PitchClass">The pitch class of the chord tone</param>
/// <param name="Function">The harmonic function of this tone in the chord</param>
public readonly record struct ChordTone(PitchClass PitchClass, ChordFunction Function);
