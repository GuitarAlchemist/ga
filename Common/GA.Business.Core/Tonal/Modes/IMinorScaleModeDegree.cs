namespace GA.Business.Core.Tonal.Modes;

using GA.Core;

// Flag interface for a minor scale degree
public interface IMinorScaleModeDegree<TSelf> : IValueObject<TSelf>
    where TSelf : struct, IValueObject<TSelf>
{
}