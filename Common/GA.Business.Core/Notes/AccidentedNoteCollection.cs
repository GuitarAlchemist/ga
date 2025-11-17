namespace GA.Business.Core.Notes;

[PublicAPI]
public sealed class AccidentedNoteCollection : LazyPrintableCollectionBase<Note.Accidented>,
    IParsable<AccidentedNoteCollection>
{
    /// <summary>
    ///     Empty <see cref="AccidentedNoteCollection" />
    /// </summary>
    public static readonly AccidentedNoteCollection Empty = new();

    public AccidentedNoteCollection(params Note.Accidented[] notes) : base(notes)
    {
    }

    public AccidentedNoteCollection(IEnumerable<Note.Accidented> notes) : base([.. notes])
    {
    }

    public AccidentedNoteCollection(IReadOnlyCollection<Note.Accidented> notes) : base(notes)
    {
    }

    #region IParsable<PitchCollection> members

    /// <inheritdoc />
    public static AccidentedNoteCollection Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, null, out var result))
        {
            throw new PitchCollectionParseException();
        }

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out AccidentedNoteCollection result)
    {
        result = Empty;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        var rawNotes = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var builder = ImmutableList.CreateBuilder<Note.Accidented>();

        foreach (var rawNote in rawNotes)
        {
            // Handle enharmonic notation
            if (rawNote.Contains('/'))
            {
                var parts = rawNote.Split('/');

                // Prefer sharps first
                var sharpOption = parts.FirstOrDefault(p => p.Contains("#"));
                var flatOption = parts.FirstOrDefault(p => p.Contains("b"));

                if (sharpOption != null && Note.Accidented.TryParse(sharpOption, provider, out var sharpNote))
                {
                    builder.Add(sharpNote);
                    continue;
                }

                if (flatOption != null && Note.Accidented.TryParse(flatOption, provider, out var flatNote))
                {
                    builder.Add(flatNote);
                    continue;
                }

                // If neither parses, fail
                return false;
            }

            if (!Note.Accidented.TryParse(rawNote, provider, out var pitch))
            {
                return false;
            }

            builder.Add(pitch);
        }

        result = new AccidentedNoteCollection(builder);
        return true;
    }

    #endregion
}
