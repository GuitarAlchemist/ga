namespace GA.Domain.Core.Theory.Atonal.Abstractions;

using Primitives;

/// <summary>
///     Abstraction for objects having a Pitch Class property
/// </summary>
/// <remarks>
///     <see cref="Note" /> | <see cref="Pitch" />
/// </remarks>
public interface IPitchClass
{
    /// <summary>
    ///     Gets the <see cref="PitchClass" />
    /// </summary>
    PitchClass PitchClass { get; }
}
