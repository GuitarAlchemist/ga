namespace GA.Business.Core.Context;

using JetBrains.Annotations;

/// <summary>
/// Notation style preference for displaying musical information
/// </summary>
[PublicAPI]
public enum NotationStyle
{
    /// <summary>Context-aware notation (uses key signature, genre, etc.)</summary>
    Auto,
    
    /// <summary>Always prefer sharps when ambiguous (C#, D#, F#, G#, A#)</summary>
    PreferSharps,
    
    /// <summary>Always prefer flats when ambiguous (Db, Eb, Gb, Ab, Bb)</summary>
    PreferFlats,
    
    /// <summary>Scientific pitch notation (C4, D5, E3, etc.)</summary>
    ScientificPitch
}
