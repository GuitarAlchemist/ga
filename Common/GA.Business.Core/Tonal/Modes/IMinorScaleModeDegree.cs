namespace GA.Business.Core.Tonal.Modes;

using GA.Core;

// Flag interface for a minor scale degree
public interface IMinorScaleModeDegree<TSelf> : IRangeValueObject<TSelf>
    where TSelf : struct, IRangeValueObject<TSelf>
{
}