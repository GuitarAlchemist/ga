namespace GA.Business.Core.Tonal.Modes.Diatonic;

using Scales;

/// <summary>
///     Abstract base class for minor scale modes.
/// </summary>
/// <typeparam name="TScaleDegree">The type of scale degree.</typeparam>
/// <remarks>
///     Minor scale modes are derived from a minor parent scale and have a clear tonal center.
///     Examples include natural minor, harmonic minor, and melodic minor scale modes.
/// </remarks>
[PublicAPI]
public abstract class MinorScaleMode<TScaleDegree>(
    Scale scale,
    TScaleDegree degree) : TonalScaleMode<TScaleDegree>(scale, degree)
    where TScaleDegree : IValueObject;
