using GA.Business.Core.Fretboard.Primitives;
using GA.Core.Combinatorics;

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
