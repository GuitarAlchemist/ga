namespace GaApi.GraphQL.Queries;

using HotChocolate.Types;
using GA.Domain.Core.Theory.Tonal;
using GA.Domain.Core.Primitives.Notes;

// ── GraphQL DTOs ─────────────────────────────────────────────────────────────

public record PitchClassDto(int Value);

public record NaturalNoteDto(int Value);

public record NoteDto(PitchClassDto PitchClass, NaturalNoteDto NaturalNote);

public record NaturalNoteWithPitchClassDto(PitchClassDto PitchClass);

public record KeyRootDto(NaturalNoteWithPitchClassDto NaturalNote);

public record PitchClassSetDto(int Value);

public record KeyDto(KeyMode KeyMode, AccidentalKind AccidentalKind, KeyRootDto Root, PitchClassSetDto PitchClassSet);

// ── Query ─────────────────────────────────────────────────────────────────────

[ExtendObjectType("Query")]
public class MusicTheoryQuery
{
    /// <summary>
    /// Get a musical key by name (e.g. "C Major", "F# Minor", "C", "F#")
    /// </summary>
    public KeyDto? GetKey(string name)
    {
        // Split "C Major" → root="C", mode="Major"
        var span = name.AsSpan().Trim();
        var spaceIdx = span.IndexOf(' ');
        var rootStr = (spaceIdx >= 0 ? span[..spaceIdx] : span).ToString();
        var modeStr = spaceIdx >= 0 ? span[(spaceIdx + 1)..].ToString() : "";

        // Key.Major/Minor.TryParse throws if root note is unparseable, so wrap in try-catch
        Key? key = null;
        try
        {
            if (!modeStr.Equals("Minor", StringComparison.OrdinalIgnoreCase))
            {
                if (Key.Major.TryParse(rootStr, out var major)) key = major;
            }
            if (key is null && !modeStr.Equals("Major", StringComparison.OrdinalIgnoreCase))
            {
                if (Key.Minor.TryParse(rootStr, out var minor)) key = minor;
            }
        }
        catch { /* unparseable root note */ }

        if (key is null) return null;

        return new KeyDto(
            key.KeyMode,
            key.AccidentalKind,
            new KeyRootDto(new NaturalNoteWithPitchClassDto(
                new PitchClassDto(key.Root.NaturalNote.PitchClass.Value))),
            new PitchClassSetDto(key.PitchClassSet.PitchClassMask));
    }

    /// <summary>
    /// Get a note by name (e.g. "C#", "Bb")
    /// </summary>
    public NoteDto? GetNote(string name)
    {
        try
        {
            var note = Note.Accidented.Parse(name, null);
            return new NoteDto(
                new PitchClassDto(note.PitchClass.Value),
                new NaturalNoteDto(note.NaturalNote.Value));
        }
        catch
        {
            return null;
        }
    }
}
