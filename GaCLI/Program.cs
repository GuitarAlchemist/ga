using GA.Business.Core.Atonal;
using GA.Core.Combinatorics;

var pitchClassCombinations = new Combinations<PitchClass>();
var lookup = pitchClassCombinations.ToLookup(pitchClass => pitchClass.ToIntervalClassVector());
var aa = pitchClassCombinations[2741];
var iv = aa.ToIntervalClassVector();

var items = lookup[iv].ToImmutableList();

/*
foreach (var vector in Fretboard.Default.RelativePositions)
{
    Console.WriteLine(vector);
}

*/