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

        var span = s.AsSpan();
        result = Empty;
        var builder = ImmutableList.CreateBuilder<Note.Accidented>();
        while (span.Length > 0)
        {
            var spaceIndex = span.IndexOf(' ');
            var segment = spaceIndex == -1 ? span : span[..spaceIndex];

            if (!Note.Accidented.TryParse(segment.ToString(), provider, out var pitch)) return false; // Fail if one item fails parsing

            builder.Add(pitch);
            span = spaceIndex == -1 ? [] : span[(spaceIndex + 1)..];
        }

        result = new AccidentedNoteCollection(builder);
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