namespace GA.Business.Core.Fretboard.Engine;

using Atonal;
using Atonal.Abstractions;
using Extensions;
using Positions;
using Primitives;

public class FretboardChordsGenerator(Fretboard fretboard)
{
    private readonly Fretboard _fretboard = fretboard ?? throw new ArgumentNullException(nameof(fretboard));

    public IEnumerable<ImmutableList<Position>> GetChordPositions(PitchClassSet? pitchClassSetFilter = null)
    {
        var relativePositions = fretboard.RelativePositions;
        var primeRelativeFretVectors = relativePositions.Where(vector => vector.IsPrime).ToImmutableList();
        foreach (var startFret in Enumerable.Range(0, 24))
        {
            foreach (var primeRelativeFretVector in primeRelativeFretVectors)
            {
                var fretVector = primeRelativeFretVector.ToFretVector(startFret);
                var positionsBuilder = ImmutableList.CreateBuilder<Position>();
                var str = Str.Min;
                foreach (var fret in fretVector)
                {
                    var location = new PositionLocation(str, fret);
                    if (!fretboard.TryGetPositionFromLocation(location, out var position)) position = fretboard.Positions.Muted[str];
                    positionsBuilder.Add(position);

                    str++;
                }

                var chordPositions = positionsBuilder.ToImmutable();
                var midiNotes =
                    chordPositions
                        .OfType<Position.Played>()
                        .Select(played => played.MidiNote)
                        .ToImmutableList();
                var pitchClassSet = midiNotes.Cast<IPitchClass>().ToPitchClassSet();
                if (pitchClassSetFilter != null)
                {
                    if (!pitchClassSet.Equals(pitchClassSetFilter)) continue;
                }
                
                yield return chordPositions;
            }
        }
    }

    public IEnumerable<Position> GetPositions(FretRange fretRange)
    {
        var positionLocationsSet = _fretboard.GetPositionLocationsSet(fretRange);

        foreach (var position in _fretboard.Positions)
        {
            switch (position)
            {
                case Position.Muted muted:
                    yield return muted; // Include muted
                    break;
                case Position.Played played:
                    var location = played.Location;
                    if (location.IsOpen) yield return position; // Include open positions
                    else if (positionLocationsSet.Contains(location)) yield return position;// Include positions in range
                    break;
            }
        }
    }
}