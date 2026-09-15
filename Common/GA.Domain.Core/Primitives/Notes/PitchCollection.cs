namespace GA.Domain.Core.Primitives.Notes;

using GA.Core.Collections;

[PublicAPI]
[CollectionBuilder(typeof(PitchCollection), nameof(Create))]
public sealed class PitchCollection(IReadOnlyCollection<Pitch> items)
    : LazyPrintableCollectionBase<Pitch>(items),
        IParsable<PitchCollection>
{
    /// <summary>
    ///     Empty <see cref="PitchCollection" />
    /// </summary>
    public static readonly PitchCollection Empty = new([]);

    public static PitchCollection Create(ReadOnlySpan<Pitch> items) => new(items.ToArray());

    #region IParsable{PitchCollection} members

    /// <inheritdoc />
    public static PitchCollection Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, null, out var result))
        {
            throw new PitchCollectionParseException();
        }

        return result;
    }

    /// <summary>
    ///     Parses space-separated pitches, each spelled with a sharp or a flat: "E2 A2 D3 G3 B3 E4", "Eb4 Eb4 G3 C3".
    /// </summary>
    public static bool TryParse(string? s, IFormatProvider? provider, out PitchCollection result)
    {
        ArgumentNullException.ThrowIfNull(s);

        result = Empty;

        var segments = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<Pitch> items = [];
        foreach (var segment in segments)
        {
            if (Pitch.Sharp.TryParse(segment, null, out var sharp))
            {
                items.Add(sharp);
            }
            else if (Pitch.Flat.TryParse(segment, null, out var flat))
            {
                items.Add(flat);
            }
            else
            {
                return false; // Fail if one item fails parsing
            }
        }

        // Success
        result = new(items);
        return true;
    }

    #endregion
}
