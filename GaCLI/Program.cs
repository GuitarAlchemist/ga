using GA.Business.Core.Fretboard;
using GA.Business.Core.Tonal;

var dorianIntervals = Mode.MajorScale.Dorian.Intervals;
var phrygianIntervals = Mode.MajorScale.Phrygian.Intervals;
var lydianIntervals = Mode.MajorScale.Lydian.Intervals;
var mixolydianIntervals = Mode.MajorScale.Mixolydian.Intervals;
var aeolianIntervals = Mode.MajorScale.Aeolian.Intervals;
var locrianIntervals = Mode.MajorScale.Locrian.Intervals;

var a = Mode.MinorScale.Aeolian.Intervals;

var fretBoard = Fretboard.Guitar();
var aa = fretBoard.OpenPositions;
var bb = fretBoard.Positions;
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard);