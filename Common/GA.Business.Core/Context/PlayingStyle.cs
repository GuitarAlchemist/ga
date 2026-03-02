namespace GA.Business.Core.Context;

using JetBrains.Annotations;

/// <summary>
/// Playing style preference
/// </summary>
[PublicAPI]
public enum PlayingStyle
{
    /// <summary>Rhythm guitar (chords, strumming)</summary>
    Rhythm,

    /// <summary>Lead guitar (solos, melodies)</summary>
    Lead,

    /// <summary>Fingerstyle (classical, folk)</summary>
    Fingerstyle,

    /// <summary>Hybrid picking (pick + fingers)</summary>
    Hybrid,

    /// <summary>Tapping techniques</summary>
    Tapping,

    /// <summary>Chord melody (jazz style)</summary>
    ChordMelody
}
