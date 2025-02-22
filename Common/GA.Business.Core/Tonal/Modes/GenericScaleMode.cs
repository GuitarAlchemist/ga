namespace GA.Business.Core.Tonal.Modes;

using Primitives;
using Scales;

public sealed class GenericScaleMode : ScaleMode<GenericScaleDegree>
{
    public GenericScaleMode(Scale parentScale, int degree) : base(parentScale, degree)
    {
        if (degree < 1 || degree > parentScale.Count)
            throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be between 1 and the number of notes in the parent scale.");

        Degree = degree;
    }

    public int Degree { get; }

    public override string Name => $"Mode {Degree}";
}