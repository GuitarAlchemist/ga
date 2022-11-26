namespace GA.Business.Core.Tonal.Modes;

using GA.Core;
using Scales;

public abstract class MinorScaleMode<TScaleDegree> : ScaleMode<TScaleDegree>
    where TScaleDegree : IValueObject
{
    protected MinorScaleMode(
        Scale scale,
        TScaleDegree degree)
            : base(scale, degree)
    {
    }
}