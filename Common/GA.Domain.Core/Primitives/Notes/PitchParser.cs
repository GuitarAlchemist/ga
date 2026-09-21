namespace GA.Domain.Core.Primitives.Notes;

internal static class PitchParser
{
    // Anchored: the whole input must be a pitch ("Eb2" is not a sharp pitch, even though it ends with "b2").
    // Octave range matches Octave (-1..9).
    private static readonly PcreRegex _sharpRegex = new(@"\A([A-G])(#?)(-1|[0-9])\z", PcreOptions.Compiled | PcreOptions.IgnoreCase);
    private static readonly PcreRegex _flatRegex = new(@"\A([A-G])(b?)(-1|[0-9])\z", PcreOptions.Compiled | PcreOptions.IgnoreCase);

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

        var regex = typeof(TAccidental) == typeof(SharpAccidental) ? _sharpRegex : _flatRegex;

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
        if (accidentalGroup.IsDefined && accidentalGroup.Value.Length > 0)
        {
            if (!TAccidental.TryParse(accidentalGroup.Value, null, out var parsedAccidental))
            {
                return false; // Never drop an accidental silently
            }

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
