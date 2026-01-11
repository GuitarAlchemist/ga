namespace GA.Business.Core.Atonal.Abstractions;

/// <summary>
///     Abstraction for objects having a Pitch Class property
/// </summary>
/// <remarks>
///     <see cref="Notes.Note" /> | <see cref="Notes.Pitch" />
/// </remarks>
public interface IPitchClass
{
    /// <summary>
    ///     Gets the <see cref="PitchClass" />
    /// </summary>
    PitchClass PitchClass { get; }
}
