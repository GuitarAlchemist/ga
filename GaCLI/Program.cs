using GA.Business.Core.Fretboard;
using GA.Business.Core.Intervals;
using GA.Business.Core.Tonal;

var dorianMode = Mode.MajorScale.Dorian;
var phrygian = Mode.MajorScale.Phrygian;
var lydian = Mode.MajorScale.Lydian;
var mixolydian = Mode.MajorScale.Mixolydian;
var aeolian = Mode.MajorScale.Aeolian;
var locrian = Mode.MajorScale.Locrian;

var s = lydian.ToString();

var fretBoard = Fretboard.Guitar();
var aa = fretBoard.OpenPositions;
var bb = fretBoard.Positions;
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard);