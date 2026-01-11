namespace GA.Business.Core.Notes;

using System;
using Intervals;
using PCRE;
using Primitives;

internal static class PitchParser
{
    public static bool TryParse<TAccidental, TKeyNote, TPitch>(
        string? s,
        Func<NaturalNote, TAccidental?, TKeyNote> createKeyNote,
        Func<TKeyNote, Octave, TPitch> createPitch,
        out TPitch result)
        where TAccidental : struct, IParsable<TAccidental>
        where TKeyNote : Note.KeyNote
        where TPitch : Pitch
    {
        result = default!;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        var regexPattern = typeof(TAccidental) == typeof(SharpAccidental)
            ? "([A-G])(#?)(10|11|[0-9])"
            : "([A-G])(b?)(10|11|[0-9])";
        var regex = new PcreRegex(regexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

        var match = regex.Match(s);
        if (!match.Success)
        {
            return false; // Failed
        }

        var noteGroup = match.Groups[1];
        var accidentalGroup = match.Groups[2];
        var octaveGroup = match.Groups[3];
        if (!noteGroup.IsDefined)
        {
            return false; // Missing note
        }

        if (!octaveGroup.IsDefined)
        {
            return false; // Missing octave
        }

        // Parse natural note
        if (!NaturalNote.TryParse(noteGroup.Value, null, out var naturalNote))
        {
            return false;
        }

        // Parse accidental
        TAccidental? accidental = null;
        if (accidentalGroup.IsDefined &&
            TAccidental.TryParse(accidentalGroup.Value, null, out var parsedAccidental))
        {
            accidental = parsedAccidental;
        }

        // Parse octave
        if (!int.TryParse(octaveGroup, out var parsedOctaveValue))
        {
            return false;
        }

        var octave = new Octave { Value = parsedOctaveValue };

        var note = createKeyNote(naturalNote, accidental);

        // Result
        result = createPitch(note, octave);
        return true;
    }
}
