using GA.Business.Core.Fretboard;

var fretBoard = Fretboard.Guitar();
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard);