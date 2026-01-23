namespace GA.Domain.Core.Theory.Tonal.Modes;

using Core.Primitives;
using JetBrains.Annotations;

/// <summary>
///     Interface for scale modes that have a tonal center.
/// </summary>
[PublicAPI]
public interface ITonalScaleMode
{
    /// <summary>
    ///     Gets the tonal center (root note) of the scale mode.
    /// </summary>
    Note TonalCenter { get; }
}
