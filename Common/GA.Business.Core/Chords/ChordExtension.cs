namespace GA.Business.Core.Chords;

/// <summary>
///     Represents the extension of a chord (7th, 9th, 11th, 13th, etc.)
/// </summary>
public enum ChordExtension
{
    /// <summary>
    ///     Basic triad (no extension)
    /// </summary>
    Triad = 0,

    /// <summary>
    ///     Seventh chord
    /// </summary>
    Seventh = 7,

    /// <summary>
    ///     Ninth chord (includes 7th)
    /// </summary>
    Ninth = 9,

    /// <summary>
    ///     Eleventh chord (includes 7th and 9th)
    /// </summary>
    Eleventh = 11,

    /// <summary>
    ///     Thirteenth chord (includes 7th, 9th, and 11th)
    /// </summary>
    Thirteenth = 13,

    /// <summary>
    ///     Add9 chord (9th without 7th)
    /// </summary>
    Add9 = 109,

    /// <summary>
    ///     Add11 chord (11th without 7th or 9th)
    /// </summary>
    Add11 = 111,

    /// <summary>
    ///     Sixth chord
    /// </summary>
    Sixth = 6,

    /// <summary>
    ///     Six-nine chord (6th and 9th)
    /// </summary>
    SixNine = 69,

    /// <summary>
    ///     Suspended second
    /// </summary>
    Sus2 = 2,

    /// <summary>
    ///     Suspended fourth
    /// </summary>
    Sus4 = 4
}
