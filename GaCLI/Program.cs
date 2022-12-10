using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard;

var combinations = PitchClassCombinations.SharedInstance;
foreach (var combination in combinations)
{
    Console.WriteLine(combination.ToString());
    var index = combination.Index;
    var index2 = combinations.GetIndex(combination);
    if (index != index2) throw new InvalidOperationException("That sucks!");
}

foreach (var vector in Fretboard.Default.RelativePositions)
{
    Console.WriteLine(vector);
}

