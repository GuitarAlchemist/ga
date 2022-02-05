using GA.Business.Core.Fretboard;
using GA.Business.Core.Tonal.Modes;

var majorModes = MajorScaleMode.All;
var harmonicMinorModes = HarmonicMinorMode.All;
var melodicMinorModes = MelodicMinorMode.All;
var majorPentatonicModes = MajorPentatonicMode.All;

var fretBoard = Fretboard.Guitar();
var aa = fretBoard.OpenPositions;
var bb = fretBoard.Positions;
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard);