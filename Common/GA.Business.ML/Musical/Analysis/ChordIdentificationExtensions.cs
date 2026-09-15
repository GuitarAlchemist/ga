namespace GA.Business.ML.Musical.Analysis;

using Core.Analysis.Voicings;
using Domain.Core.Primitives.Notes;
using Domain.Core.Theory.Atonal;

public static class ChordIdentificationExtensions
{
    /// <summary>
    ///     Reads the root pitch class out of <see cref="ChordIdentification.RootPitchClass" />.
    /// </summary>
    /// <remarks>
    ///     Despite its name, <see cref="ChordIdentification.RootPitchClass" /> holds the root <b>note name</b>
    ///     ("A", "Bb", "F#") whenever the recognizer found a root. Only when it did not
    ///     (<see cref="ChordIdentification.MatchDistance" /> = -1: empty set or Forte fallback) does it hold the
    ///     bass pitch class in set notation ("0".."9", "T", "E"). <see cref="PitchClass.TryParse" /> reads set
    ///     notation, where "A" = 10 and "E" = 11, so it must not be used on note names.
    /// </remarks>
    public static bool TryGetRootPitchClass(this ChordIdentification chordId, out PitchClass pitchClass)
    {
        var root = chordId.RootPitchClass;
        if (chordId.MatchDistance != -1 && Note.Accidented.TryParse(root, null, out var note))
        {
            pitchClass = note.PitchClass;
            return true;
        }

        return PitchClass.TryParse(root, null, out pitchClass);
    }
}
