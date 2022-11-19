namespace GA.Business.Core.Notes;

using GA.Core;
using GA.Core.Collections;

[PublicAPI]
public class PitchCollection : LazyPrintableCollectionBase<Pitch>, IParsable<PitchCollection>
{
    #region IParsable{PitchCollection} members

    public static PitchCollection Parse(string s, IFormatProvider? provider) => Parse(s);
    public static bool TryParse(string? s, IFormatProvider? provider, out PitchCollection result) => TryParse(s, out result);

    #endregion

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

    public PitchCollection(params Pitch[] items) : base(items) { }
    private PitchCollection(IReadOnlyCollection<Pitch> items) : base(items) { }
}