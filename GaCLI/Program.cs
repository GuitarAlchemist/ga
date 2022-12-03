using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard;

//var relFretVectors = new RelativeFretVectorCollection();
//var equiv = relFretVectors.Equivalences;
//var normalized = relFretVectors.Normalized;
//var translated = relFretVectors.Translated;

var aa = Fretboard.Default;

foreach (var variation in PitchClassVariations.SharedInstance)
{
    Console.WriteLine(variation.ToString());
}