namespace GA.Business.Core.Fretboard.Engine;

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.Text;

using Primitives;
using Tonal;

/// <summary>
/// Collection of <see cref="KeyPosition"/> items.
/// </summary>
public class KeyPositionCollection : IReadOnlyCollection<KeyPosition>
{
    private readonly IReadOnlyCollection<KeyPosition> _keyPositions;

    /// <summary>
    /// Create a <see cref="KeyPositionCollection"/> instance.
    /// </summary>
    /// <param name="key">The <see cref="Key"/>.</param>
    /// <param name="positions">The <see cref="Position.Fretted"/> collection</param>
    /// <exception cref="ArgumentNullException">Thrown when a parameter is null.</exception>
    public KeyPositionCollection(
        Key key,
        IReadOnlyCollection<Position.Fretted> positions)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        if (positions == null) throw new ArgumentNullException(nameof(positions));

        _keyPositions = GetKeyPositions(key, positions);
    }

    /// <summary>
    /// Gets the <see cref="Key"/>
    /// </summary>
    public Key Key { get; }

    public IEnumerator<KeyPosition> GetEnumerator() => _keyPositions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _keyPositions).GetEnumerator();
    public int Count => _keyPositions.Count;

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var keyPosition in _keyPositions)
        {
            if (sb.Length > 0) sb.Append(", ");
            var keyNote = keyPosition.KeyNote;
            var position = keyPosition.Position;
            var s = $"str {position.Str} ({keyNote})";
            sb.Append(s);
        }

        return sb.ToString();
    }

    private static ReadOnlyCollection<KeyPosition> GetKeyPositions(
        Key key,
        IEnumerable<Position.Fretted> positions)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (positions == null) throw new ArgumentNullException(nameof(positions));

        var keyNotes = key.GetNotes();
        var noteByPitchClass = 
            keyNotes.ToImmutableDictionary(
                note => note.PitchClass, 
                note => note);

        var list = new List<KeyPosition>();
        foreach (var position in positions)
        {
            var keyNote = noteByPitchClass[position.Pitch.PitchClass];
            var keyPosition = new KeyPosition(position, keyNote);
            list.Add(keyPosition);
        }

        return list.AsReadOnly();
    }
}