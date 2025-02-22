using GA.Business.Config;
using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Tonal.Modes;
using GA.Core.Combinatorics;

var combinations = new Combinations<PitchClass>(PitchClass.Items);
foreach (var combination in combinations)
{
    Console.WriteLine(combination.ToString());
    var index = combination.Index;
    var index2 = combinations.GetIndex(combination);
    if (index != index2) throw new InvalidOperationException("That sucks!");
}

// ----------------------------------------------------------------

foreach (var vector in Fretboard.Default.RelativePositions)
{
    Console.WriteLine(vector);
}

// ----------------------------------------------------------------

var modes = MajorScaleMode.Items;

foreach (var mode in modes)
{
    Console.WriteLine($@"{mode.Name} mode");
    Console.WriteLine($@"Items notes   : {mode.Notes}");
    Console.WriteLine($@"Color notes : {mode.ColorNotes}");
    Console.WriteLine($@"Formula     : {mode.Formula}");
    Console.WriteLine("");       
}

// ----------------------------------------------------------------

var instrument = InstrumentsConfig.Instruments.Ukulele;
foreach (var prop in instrument.GetType().GetProperties())
{
    Console.WriteLine(prop.Name);
}
