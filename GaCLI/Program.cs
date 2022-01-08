using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Tonal;

var notes = Key.Major.G.GetNotes();

var fretBoard = Fretboard.Guitar();
fretBoard.CapoFret = 4;
var aa = fretBoard.OpenPositions;
var bb = fretBoard.Positions;
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard);