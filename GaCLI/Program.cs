using GA.Business.Core.Atonal;
using GA.Business.Core.Extensions;
using GA.Core.Combinatorics;

var combinations = new Combinations<PitchClass>();
var lookup = combinations.ToLookup(pitchClass => pitchClass.ToIntervalClassVector());
var majorScale = combinations[2741];
var majorScaleIntervalStructure = majorScale.ToIntervalStructure();
var majorScaleIntervalVector = majorScale.ToIntervalClassVector();

var items = lookup[majorScaleIntervalVector].ToImmutableList();

/*
foreach (var vector in Fretboard.Default.RelativePositions)
{
    Console.WriteLine(vector);
}

*/