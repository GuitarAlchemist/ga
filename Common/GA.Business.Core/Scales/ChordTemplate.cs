namespace GA.Business.Core.Scales;

using Atonal;

public class ChordTemplate(PitchClassSet pitchClassSet)
{
    public PitchClassSet PitchClassSet { get; } = pitchClassSet;
    public ImmutableHashSet<Scale> AssociatedScales { get; private set; } = [];

    public void AddAssociatedScale(Scale scale)
    {
        AssociatedScales = AssociatedScales.Add(scale);
    }

    public override string ToString()
    {
        return $"Chord: {PitchClassSet}";
        // return $"Chord: {PitchClassSet}, Scales: {string.Join(", ", AssociatedScales.Select(s => s.Name))}";
    }
}
