namespace GA.Business.Core.Notes;

[PublicAPI]
public sealed class AccidentedNoteCollection : LazyPrintableCollectionBase<Note.Accidented>, IParsable<AccidentedNoteCollection>
{
    #region IParsable<PitchCollection> members

    /// <inheritdoc />
    public static AccidentedNoteCollection Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, null, out var result)) throw new PitchCollectionParseException();
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out AccidentedNoteCollection result)
    {
        ArgumentNullException.ThrowIfNull(s);
        
        result = Empty;

        var segments = s.Split(" ");
        var items = new List<Note.Accidented>();
        foreach (var segment in segments)
        {
            if (!Note.Accidented.TryParse(segment, null, out var pitch)) return false; // Fail if one item fails parsing
            items.Add(pitch);
        }

        // Success
        result = new(items);
        return true;
    }

    #endregion

    /// <summary>
    /// Empty <see cref="AccidentedNoteCollection"/>
    /// </summary>
    public static readonly AccidentedNoteCollection Empty = new();
   
    public AccidentedNoteCollection(params Note.Accidented[] items) : base(items) { }
    public AccidentedNoteCollection(IEnumerable<Note.Accidented> items) : base(items.ToImmutableList()) { }
    public AccidentedNoteCollection(IReadOnlyCollection<Note.Accidented> items) : base(items) { }
}