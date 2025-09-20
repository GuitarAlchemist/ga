﻿namespace GA.Business.Core.Tonal.Modes;

/// <summary>
/// Interface for scale mode factory methods
/// </summary>
/// <typeparam name="TScaleMode">The scale mode type</typeparam>
/// <typeparam name="TScaleDegree">The scale degree type</typeparam>
[PublicAPI]
public interface IScaleModeFactory<out TScaleMode, in TScaleDegree>
    where TScaleMode : ScaleMode<TScaleDegree>
    where TScaleDegree : IValueObject
{
    /// <summary>
    /// Gets all instances of the scale mode
    /// </summary>
    static abstract IEnumerable<TScaleMode> Items { get; }

    /// <summary>
    /// Gets a scale mode by its degree
    /// </summary>
    /// <param name="degree">The scale degree</param>
    /// <returns>The scale mode</returns>
    static abstract TScaleMode Get(TScaleDegree degree);

    /// <summary>
    /// Gets a scale mode by its degree value
    /// </summary>
    /// <param name="degree">The scale degree value</param>
    /// <returns>The scale mode</returns>
    static abstract TScaleMode Get(int degree);
}
