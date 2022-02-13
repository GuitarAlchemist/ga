using GA.Business.Core.Fretboard;
using GA.Business.Core.Tonal.Modes;
using GA.Business.Core.Tonal.Primitives;

var majorModes = MajorScaleMode.All;
var harmonicMinorModes = HarmonicMinorMode.All;
var melodicMinorModes = MelodicMinorMode.All;

foreach (var mode in majorModes)
{
    Console.WriteLine(mode.Name);
    Console.WriteLine("Notes:     " + mode.Notes);
    Console.WriteLine("Intervals: " + mode.Intervals);
    Console.WriteLine("Formula:   " + mode.Formula.ToString().Trim());
    Console.WriteLine("Identity:  " + mode.Identity);
    Console.WriteLine("Listen:    " + $"https://ianring.com/musictheory/scales/{mode.Identity}");
    Console.WriteLine();
}

var ionianIdentity = MajorScaleMode.Get(MajorScaleDegree.Ionian).Identity;
var dorianIdentity = MajorScaleMode.Get(MajorScaleDegree.Dorian).Identity;

/*

Ideas: 
Decompose horizontal movement into m3/M3 fret intervals - See https://www.youtube.com/watch?v=Ab3nqlbl9us

 */

var majorPentatonicModes = MajorPentatonicMode.All;

var fretBoard = Fretboard.Guitar();
var aa = fretBoard.OpenPositions;
var bb = fretBoard.Positions;
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard); // m3/M3 - 

