namespace GA.Business.Core.Atonal;

using GA.Core.Combinatorics;

/// <summary>
/// Computes all possible combinations of pitch classes.
/// </summary>
public class PitchClassVariations : IReadOnlyCollection<Variation<PitchClass>>
{
    public static readonly PitchClassVariations SharedInstance = new();

    private readonly VariationsWithRepetitions<bool> _boolVariations;

    public PitchClassVariations()
    {
        _boolVariations = new(new[] {false, true}, PitchClass.Values.Count);
    }

    public IEnumerator<Variation<PitchClass>> GetEnumerator()
    {
        foreach (var boolVariation in _boolVariations)
        {
            var pitchClass = PitchClass.Min;
            var pitchClassArrayBuilder = ImmutableArray.CreateBuilder<PitchClass>();
            foreach (var b in boolVariation)
            {
                if (b) pitchClassArrayBuilder.Add(pitchClass);
                pitchClass++;
            }

            var pitchClassArray = pitchClassArrayBuilder.ToImmutable();
            yield return new(boolVariation.Index, pitchClassArray);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => (int) _boolVariations.Count;
}