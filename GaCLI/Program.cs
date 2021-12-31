using GA.Business.Core.Fretboard;

var fretBoard = Fretboard.Ukulele();
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard);