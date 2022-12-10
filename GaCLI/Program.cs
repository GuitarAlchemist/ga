using GA.Business.Core.Atonal;

var combinations = PitchClassCombinations.SharedInstance;
foreach (var combination in combinations)
{
    Console.WriteLine(combination.ToString());
    var index = combination.Index;
    var index2 = combinations.GetIndex(combination);
    if (index != index2) throw new InvalidOperationException("That sucks!");
}

//foreach (var variation in FingerVariations.SharedInstance)
//{
//    Console.WriteLine(variation.ToString());
//}

//foreach (var vector in Fretboard.Default.RelativePositions)
//{
//    Console.WriteLine(vector);
//}

