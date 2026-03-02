namespace GA.Domain.Core.Primitives.Notes;

using System.Runtime.CompilerServices;
using GA.Core.Collections;

/// <summary>
/// An ordered set of chromatic notes
/// </summary>
/// <param name="notes"></param>
[PublicAPI]
[CollectionBuilder(typeof(ChromaticNoteSet), nameof(Create))]
public sealed class ChromaticNoteSet(ReadOnlySpan<Note.Chromatic> notes)
    : PrintableImmutableSet<Note.Chromatic>(notes)
{
    /// <summary>
    ///     Empty <see cref="ChromaticNoteSet" />
    /// </summary>
    public static readonly ChromaticNoteSet Empty = Create([]);

    public static ChromaticNoteSet Create(ReadOnlySpan<Note.Chromatic> notes) => new(notes);
}
