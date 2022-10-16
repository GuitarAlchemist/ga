using GA.Business.Core.Tonal.Modes;

var modes = MajorScaleMode.Objects;
var modes1 = MelodicMinorMode.Objects;

foreach (var mode in modes)
{
    Console.WriteLine($@"{mode.Name} mode");
    Console.WriteLine($@"All notes   : {mode.Notes}");
    Console.WriteLine($@"Color notes : {mode.ColorNotes}");
    Console.WriteLine($@"Formula     : {mode.Formula}");
    Console.WriteLine("");       
}