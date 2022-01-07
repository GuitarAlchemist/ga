namespace GA.Business.Core.Notes;

using System.Collections;
using System.Collections.Immutable;

[PublicAPI]
public class PitchCollection : IReadOnlyCollection<Pitch>
{
    public static readonly PitchCollection Empty = new();

    public static PitchCollection Parse(string s)
    {
        if (!TryParse(s, out var result)) throw new PitchCollectionParseException();
        return result;
    }
    
    public static bool TryParse(string s, out PitchCollection parsedPitchCollection)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
        parsedPitchCollection = Empty;

        var segments = s.Split(" ");
        var items = new List<Pitch>();
        foreach (var segment in segments)
        {
            if (!Pitch.Sharp.TryParse(segment, out var pitch)) return false; // Fail if one item fails parsing
            items.Add(pitch);
        }

        // Success
        parsedPitchCollection = new(items);
        return true;
    }

    public PitchCollection(params Pitch[] items) : this(items.ToImmutableList()) { }
    public IEnumerator<Pitch> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _items.Count;

    private readonly IReadOnlyCollection<Pitch> _items;
    private PitchCollection() => _items = ImmutableList<Pitch>.Empty;
    private PitchCollection(IEnumerable<Pitch> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        _items = items.ToImmutableList();
    }
}