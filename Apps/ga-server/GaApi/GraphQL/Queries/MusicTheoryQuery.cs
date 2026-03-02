namespace GaApi.GraphQL.Queries;

using HotChocolate.Types;
using GA.Domain.Core.Theory.Tonal;
using GA.Domain.Core.Primitives.Notes;

[ExtendObjectType("Query")]
public class MusicTheoryQuery
{
    /// <summary>
    /// Get a musical key by name (e.g. "C Major", "F# Minor", "Am", "Eb")
    /// </summary>
    public Key? GetKey(string name)
    {
        if (Key.Major.TryParse(name, out var major)) return major;
        if (Key.Minor.TryParse(name, out var minor)) return minor;
        return null;
    }

    /// <summary>
    /// Get a note by name (e.g. "C#", "Bb")
    /// </summary>
    public Note.Accidented? GetNote(string name)
    {
        try
        {
            return Note.Accidented.Parse(name, null);
        }
        catch
        {
            return null;
        }
    }
}
