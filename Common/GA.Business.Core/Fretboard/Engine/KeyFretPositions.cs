using System.Collections.ObjectModel;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.SetTheory;
using GA.Business.Core.Tonal;

namespace GA.Business.Core.Fretboard.Engine;

/// <summary>
/// (Fret, Key, KeyPositionCollection) tuple
/// </summary>
/// <remarks>
/// Goal:
/// Declare a relationship between Open fret/capo position and keys
/// The objective is surfacing "opportunities" to the user - Example
/// The fretboard context is required (Tuning)
/// 1) Open position works best for certain keys => List keys (Keys with the highest open positions count first) 
/// 2) Capo position =>  List keys (Keys with the most open positions first)
/// 3) Given key => List the capo positions/open position (Capo positions with the highest open positions count first)
/// </remarks>
public record KeyFretPositions(
    Fret Fret,
    Key Key,
    KeyPositionCollection KeyPositions)
{
    /// <summary>
    /// Create a collection of <see cref="KeyFretPositions"/> items.
    /// </summary>
    /// <param name="fretboard">The <see cref="Fretboard"/></param>
    /// <returns>The <see cref="ReadOnlyCollection{KeyFretPositions}"/></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ReadOnlyCollection<KeyFretPositions> CreateCollection(Fretboard fretboard)
    {
        if (fretboard == null) throw new ArgumentNullException(nameof(fretboard));

        var list = new List<KeyFretPositions>();

        foreach (var key in Key.GetAll())
        {
            var keyNotes = key.GetNotes();
            var keyPitchClassSet = new HashSet<PitchClass>(keyNotes.Select(note => note.PitchClass));

            foreach (var fret in fretboard.Frets)
            {
                var positions = new List<Position.Fretted>();
                foreach (var position in fretboard[fret])
                {
                    var pitchClass = position.Pitch.PitchClass;
                    if (keyPitchClassSet.Contains(pitchClass)) positions.Add(position);
                }

                var keyPositions = new KeyPositionCollection(key, positions);
                list.Add(new(fret, key, keyPositions));
            }
        }

        return list.AsReadOnly();
    }
}