namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using System;
using System.Linq;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Primitives;

public static class VoicingValidator
{
    /// <summary>
    /// Returns true when two or more played notes land on the same guitar string.
    /// Such a voicing is physically impossible.
    /// </summary>
    public static bool HasDuplicateStrings(Voicing voicing)
    {
        var strings = voicing.Positions
            .OfType<Position.Played>()
            .Select(p => p.Location.Str.Value)
            .ToList();

        return strings.Count != strings.Distinct().Count();
    }

    public static bool IsPhysicallyValid(Voicing voicing) => !HasDuplicateStrings(voicing);

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when the voicing has duplicate strings.
    /// </summary>
    public static void ThrowIfInvalid(Voicing voicing)
    {
        if (HasDuplicateStrings(voicing))
            throw new InvalidOperationException(
                $"Invalid voicing: multiple notes on the same string. " +
                $"Diagram: {string.Join("-", voicing.Positions.Select(p =>
                    p is Position.Played pl ? pl.Location.Fret.Value.ToString() : "x"))}");
    }
}
