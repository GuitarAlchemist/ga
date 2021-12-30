using GA.Business.Core.Fretboard;

var fretBoard = Fretboard.Default;
var positionsByStr = fretBoard.Positions.ToLookup(position => position.Str);
var strings = fretBoard.Strings;
foreach (var str in strings)
{
    Console.Write($"Str {str}: ");
    var stringPositions = positionsByStr[str];
    foreach (var position in stringPositions)
    {
        Console.Write($"{position} ");
    }
    Console.WriteLine();
}
