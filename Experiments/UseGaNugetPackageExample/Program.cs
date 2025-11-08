using GA.Business.Config;
using GA.Business.Core.Atonal;
using GA.Core.Combinatorics;

var combinations = new Combinations<PitchClass>(PitchClass.Items);
foreach (var combination in combinations)
{
    Console.WriteLine(combination.ToString());
    var index = combination.Index;
    var index2 = combinations.GetIndex(combination);
    if (index != index2)
    {
        throw new InvalidOperationException("That sucks!");
    }
}

// ----------------------------------------------------------------

// TODO: Implement Fretboard.Default
// foreach (var vector in Fretboard.Default.RelativePositions)
// {
//     Console.WriteLine(vector);
// }

// ----------------------------------------------------------------

// TODO: Implement MajorScaleMode.Items
// var modes = MajorScaleMode.Items;
// var modes = new List<object>(); // Temporary stub

// foreach (var mode in modes)
// {
//     Console.WriteLine($@"{mode.Name} mode");
//     Console.WriteLine($@"Items notes   : {mode.Notes}");
//     Console.WriteLine($@"Characteristic notes : {mode.CharacteristicNotes}");
//     Console.WriteLine($@"Formula     : {mode.Formula}");
//     Console.WriteLine("");
// }

// ----------------------------------------------------------------

// Get all instruments using the new YamlDotNet-based API
var instruments = InstrumentsConfig.getAllInstruments();
var ukulele = instruments.FirstOrDefault(i => i.Name == "Ukulele");
if (ukulele != null)
{
    Console.WriteLine($"Instrument: {ukulele.Name}");
    foreach (var tuning in ukulele.Tunings)
    {
        Console.WriteLine($"  Tuning: {tuning.Name} - {tuning.Tuning}");
    }
}
else
{
    Console.WriteLine("Ukulele not found in instruments configuration");
}
