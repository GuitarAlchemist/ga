namespace GA.Business.Core.Tonal.Primitives;

using JetBrains.Annotations;

/// <summary>
///     Interface for mapping scale degrees to their functional roles
/// </summary>
/// <remarks>
///     Provides a method to convert a scale degree to its functional role (Tonic, Dominant, etc.)
///     Implemented by various scale degree types such as MajorScaleDegree, NaturalMinorScaleDegree, etc.
/// </remarks>
[PublicAPI]
public interface IScaleDegreeFunctionMapping
{
    /// <summary>
    ///     Maps the scale degree to its functional role in the scale
    /// </summary>
    /// <returns>The <see cref="ScaleDegreeFunction" /> representing the functional role (Tonic, Dominant, etc.)</returns>
    ScaleDegreeFunction ToFunction();
}
