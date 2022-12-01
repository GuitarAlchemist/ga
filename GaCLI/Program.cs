using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard.Positions;
using GA.Core.Combinatorics;

// var a = Fretboard.Default.Positions.Played[PositionLocation.Open(Str.Min)];

var relativeFretVariations = new RelativeFretVariations();
var variationByIndex = relativeFretVariations.ToIndexDictionary();
var index = relativeFretVariations.GetIndex(variationByIndex[1000]);
foreach (var pair in variationByIndex)
{
    // dictBuilder.Add(variation.Index, v);
    Console.WriteLine(pair.Value.ToString());
}


// --

// --

foreach (var variation in PitchClassVariations.SharedInstance)
{
    Console.WriteLine(variation.ToString());
}

