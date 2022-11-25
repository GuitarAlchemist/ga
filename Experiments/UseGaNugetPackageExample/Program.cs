using System.Collections.Immutable;
using GA.Business.Config;
using GA.Business.Core.Tonal.Modes;
using GA.Business.Core.Fretboard.Primitives;
using GA.Core.Combinatorics;

var modes = MajorScaleMode.Objects;

foreach (var mode in modes)
{
    Console.WriteLine($@"{mode.Name} mode");
    Console.WriteLine($@"All notes   : {mode.Notes}");
    Console.WriteLine($@"Color notes : {mode.ColorNotes}");
    Console.WriteLine($@"Formula     : {mode.Formula}");
    Console.WriteLine("");       
}

// -----------------------------------------------------------------

var a = Instruments.Instrument;

var instrument = Instruments.Instrument.Ukulele;

foreach (var prop in instrument.GetType().GetProperties())
{
    Console.WriteLine(prop.Name);
}

// -----------------------------------------------------------------

var moveableVariations = new MoveableVariations();
var normalForms = new List<Variation<Fret>>();
foreach (var variation in moveableVariations)
{
    var index = moveableVariations.GetIndex(variation);

    if (index != variation.Index) throw new InvalidOperationException("That sucks!");

    var isNormalForm = variation.Any(fret => fret == Fret.One);
    if (!isNormalForm) continue;
    normalForms.Add(variation);
}

var variationsCount = moveableVariations.Count;
var variationsArray  = moveableVariations.ToImmutableArray();

// Console.WriteLine($"{string.Join(" ", movable)} => {variations.Count} variations");
foreach (var variation in moveableVariations)
{
    Console.WriteLine(variation.ToString());
}

/// <summary>
/// - Muted fret + frets 1..5;
/// - 6 string
/// </summary>
public sealed class MoveableVariations : VariationsWithRepetitions<Fret>
{
    public MoveableVariations(int strCount = 6) 
        : base(Fret.Set(-1, 1..5), strCount)
    {
    }
}
