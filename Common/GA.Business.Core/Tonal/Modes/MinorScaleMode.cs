namespace GA.Business.Core.Tonal.Modes;

using GA.Core;
using Scales;

public abstract class MinorScaleMode<TScaleDegree>(Scale scale,
    TScaleDegree degree) : ScaleMode<TScaleDegree>(scale, degree)
    where TScaleDegree : IValueObject;