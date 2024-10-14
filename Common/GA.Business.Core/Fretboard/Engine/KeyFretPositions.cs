namespace GA.Business.Core.Fretboard.Engine;

using Primitives;
using Tonal;
using Atonal;

/// <summary>
/// (Fret, Key, KeyPositionCollection) tuple
/// </summary>
/// <remarks>
/// Goal:
/// Declare an equivalence between Open fret/capo position and keys
/// The objective is surfacing "opportunities" to the user - Example
/// The fretboard context is required (Tuning)
/// 1) Open position works best for certain keys => List keys (Keys with the highest open positions count first) 
/// 2) Capo position =>  List keys (Keys with the most open positions first)
/// 3) Given key => List the capo positions/open position (Capo positions with the highest open positions count first)
/// </remarks>
public sealed record KeyFretPositions(
    Fret Fret,
    Key Key,
    KeyPositionCollection KeyPositions)
{
    #region Static Helpers
    
    /// <summary>
    /// Create a collection of <see cref="KeyFretPositions"/> items.
    /// </summary>
    /// <param name="fretboard">The <see cref="Fretboard"/></param>
    /// <returns>The <see cref="ReadOnlyCollection{KeyFretPositions}"/></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ReadOnlyCollection<KeyFretPositions> CreateCollection(Fretboard fretboard)
    {
        ArgumentNullException.ThrowIfNull(nameof(fretboard));

        var list = new List<KeyFretPositions>();
        foreach (var key in Key.Items)
        {
            var keyNotes = key.Notes;
            var keyPitchClassSet = new HashSet<PitchClass>(keyNotes.Select(note => note.PitchClass));

            foreach (var fret in fretboard.Frets)
            {
                var positions = new List<Position.Played>();
                foreach (var position in fretboard.Positions.Played[fret])
                {
                    var pitchClass = position.MidiNote.PitchClass;
                    if (keyPitchClassSet.Contains(pitchClass)) positions.Add(position);
                }

                var keyPositions = new KeyPositionCollection(key, positions);
                list.Add(new(fret, key, keyPositions));
            }
        }

        return list.AsReadOnly();
    }
    
    #endregion
}