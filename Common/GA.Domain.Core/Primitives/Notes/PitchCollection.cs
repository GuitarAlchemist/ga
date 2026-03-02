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

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out PitchCollection result)
    {
        ArgumentNullException.ThrowIfNull(s);

        result = Empty;

        var segments = s.Split(" ");
        List<Pitch> items = [];
        foreach (var segment in segments)
        {
            if (!Pitch.Sharp.TryParse(segment, null, out var pitch))
            {
                return false; // Fail if one item fails parsing
            }

            items.Add(pitch);
        }

        // Success
        result = new(items);
        return true;
    }

    #endregion
}
