namespace GA.Business.Core.Context;

using JetBrains.Annotations;

/// <summary>
/// Enharmonic preference for choosing between equivalent note spellings
/// </summary>
[PublicAPI]
public enum EnharmonicPreference
{
    /// <summary>Use musical context (key, scale, genre) to decide</summary>
    Context,

    /// <summary>Always prefer sharps (C# over Db)</summary>
    Sharps,

    /// <summary>Always prefer flats (Db over C#)</summary>
    Flats,

    /// <summary>Use simplest notation (fewest accidentals)</summary>
    Simplest
}
