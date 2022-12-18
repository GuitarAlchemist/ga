using GA.Business.Core.Atonal;
using GA.Business.Core.Extensions;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Primitives;
using GA.Core.Combinatorics;

var fretboard = Fretboard.Default;
var fretRange = Fret.Range(Fret.Open, 12);
var rp = fretboard.RelativePositions;
var count = 0;
foreach (var startFret in fretRange)
{
    foreach (var relativeFretVector in rp)
    {
        if (!relativeFretVector.IsPrime) continue;
        var fretVector = relativeFretVector.ToFretVector(startFret);
        var positionLocations = fretVector.PositionLocations;
        var fretVectorPositions = fretboard.Positions.Played.FromLocations(positionLocations);

        var aa = fretVectorPositions.Select(played => played.MidiNote.ToPitch()).ToImmutableList();

        count++;
    }
}

// -------------------------------------

var combinations = new Combinations<PitchClass>();
var majorScale = combinations[2741];
var majorScaleIntervalStructure = majorScale.ToIntervalStructure();
var majorScaleIntervalVector = majorScale.ToIntervalClassVector();

var icvLookup = combinations.ToLookup(pitchClasses => pitchClasses.ToIntervalClassVector());
var icvMembers = icvLookup[majorScaleIntervalVector].ToImmutableList();

var isLookup = combinations.ToLookup(pitchClasses => pitchClasses.ToIntervalStructure());
var isMembers = isLookup[majorScaleIntervalStructure].ToImmutableList();

var dummy = 1;

/*
foreach (var vector in Fretboard.Default.RelativePositions)
{
    Console.WriteLine(vector);
}

*/