using GA.Business.Core.Atonal;
using GA.Business.Core.Extensions;
using GA.Core.Combinatorics;

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