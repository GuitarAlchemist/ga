using GA.Business.Config;
using GA.Business.Core.Tonal.Modes;

var modes = MajorScaleMode.Objects;

foreach (var mode in modes)
{
    Console.WriteLine($@"{mode.Name} mode");
    Console.WriteLine($@"All notes   : {mode.Notes}");
    Console.WriteLine($@"Color notes : {mode.ColorNotes}");
    Console.WriteLine($@"Formula     : {mode.Formula}");
    Console.WriteLine("");       
}

var a = Instruments.Instrument;

var instrument = Instruments.Instrument.Ukulele;

foreach (var prop in instrument.GetType().GetProperties())
{
    Console.WriteLine(prop.Name);
}