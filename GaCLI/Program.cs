using GA.Business.Core.Fretboard.Primitives;
using GA.Core.Combinatorics;

var movable = Fret.Set(-1, 1..5);
var strCount = 6;
var variations = new VariationsWithRepetitions<Fret>(movable, strCount);

Console.WriteLine($"{string.Join(" ", movable)} => {variations.Count} variations");
foreach (var variation in variations)
{
    var sVariation = string.Join(" ", variation);
    Console.WriteLine(sVariation);
}