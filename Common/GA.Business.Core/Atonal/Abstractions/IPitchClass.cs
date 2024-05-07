namespace GA.Business.Core.Atonal.Abstractions;

/// <summary>
/// Abstraction for an object that has a pitch class
/// </summary>
public interface IPitchClass
{
    /// <summary>
    /// Gets the <see cref="PitchClass"/>
    /// </summary>
    PitchClass PitchClass { get; }
}