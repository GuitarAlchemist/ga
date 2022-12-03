using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard.Positions;

var relFretVectors = new RelativeFretVectorCollection();
var equiv = relFretVectors.Equivalences;
var normalized = relFretVectors.Normalized;
var translated = relFretVectors.Translated;


// --

// --

foreach (var variation in PitchClassVariations.SharedInstance)
{
    Console.WriteLine(variation.ToString());
}