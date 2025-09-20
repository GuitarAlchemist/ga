﻿namespace GA.Business.Core.Tonal.Primitives;

/// <summary>
/// The function of a scale degree
/// </summary>
/// <remarks>
/// See https://en.wikipedia.org/wiki/Degree_(music)
/// https://music.utk.edu/theorycomp/courses/murphy/documents/Major+MinorScales.pdf
///
///                                 Tonic
///                        Subtonic       Supertonic
///             Submediant                           Mediant
/// Subdominant                                              Dominant
///
/// </remarks>
public enum ScaleDegreeFunction
{
    /// <summary>
    /// Tonal center, note of final resolution
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Tonic_(music)
    /// </remarks>
    Tonic,

    /// <summary>
    /// One whole step above the tonic
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Supertonic
    /// </remarks>
    Supertonic,

    /// <summary>
    /// Midway between tonic and dominant, (in minor key) root of relative major key
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Mediant
    /// </remarks>
    Mediant,

    /// <summary>
    /// Lower dominant, same interval below tonic as dominant is above tonic
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Subdominant
    /// </remarks>
    Subdominant,

    /// <summary>
    /// Second in importance to the tonic
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Dominant_(music)
    /// </remarks>
    Dominant,

    /// <summary>
    /// Lower mediant, midway between tonic and subdominant, (in major key) root of relative minor key
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Submediant
    /// </remarks>
    Submediant,

    /// <summary>
    /// One whole step below tonic in natural minor scale.
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Subtonic
    /// </remarks>
    Subtonic,

    /// <summary>
    /// One half step below tonic. Melodically strong affinity for and leads to tonic
    /// </summary>
    /// <remarks>
    /// See https://en.wikipedia.org/wiki/Leading-tone
    /// </remarks>
    LeadingTone,

    /// <summary>
    /// Function that doesn't fit into the traditional diatonic functions
    /// </summary>
    /// <remarks>
    /// Used for non-diatonic scales or modes where traditional functions don't apply
    /// </remarks>
    Other
}

