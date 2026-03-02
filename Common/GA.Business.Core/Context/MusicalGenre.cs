namespace GA.Business.Core.Context;

using JetBrains.Annotations;

/// <summary>
/// Musical genre context for style-appropriate suggestions
/// </summary>
[PublicAPI]
public enum MusicalGenre
{
    /// <summary>Rock music</summary>
    Rock,

    /// <summary>Jazz music</summary>
    Jazz,

    /// <summary>Blues music</summary>
    Blues,

    /// <summary>Classical music</summary>
    Classical,

    /// <summary>Metal music</summary>
    Metal,

    /// <summary>Folk music</summary>
    Folk,

    /// <summary>Country music</summary>
    Country,

    /// <summary>Funk music</summary>
    Funk,

    /// <summary>Soul music</summary>
    Soul,

    /// <summary>R&B music</summary>
    RAndB,

    /// <summary>Pop music</summary>
    Pop,

    /// <summary>Fusion/experimental</summary>
    Fusion
}
