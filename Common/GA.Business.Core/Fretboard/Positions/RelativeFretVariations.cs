namespace GA.Business.Core.Fretboard.Positions;

using Primitives;
using GA.Core.Combinatorics;

/// TODO: Some variations can be reduced to "canonical" form (Min relative fret = 0) + translation
/// TODO: Establish relationships between these canonical forms and regular form + provide translation property
/// TODO: Extract some metric or grouping key for the translation property
public sealed class RelativeFretVariations : VariationsWithRepetitions<RelativeFret>
{
    public RelativeFretVariations(int strCount = 6) 
        : base(RelativeFret.Items, strCount)
    {
    }
}