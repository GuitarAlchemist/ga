namespace GA.Business.Core.Tonal.Modes;

using Scales;

public abstract class MinorScaleMode<TScaleDegree> : ScaleMode<TScaleDegree>
    where TScaleDegree : IReadOnlyValue
{
    protected MinorScaleMode(
        Scale scale,
        TScaleDegree degree)
        : base(scale, degree)
    {
    }
}