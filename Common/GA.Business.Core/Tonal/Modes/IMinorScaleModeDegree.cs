namespace GA.Business.Core.Tonal.Modes;

/// <summary>
///     Marker interface for a minor scale degree
/// </summary>
/// <typeparam name="TSelf">The concrete type</typeparam>
public interface IMinorScaleModeDegree<TSelf> : IRangeValueObject<TSelf>
    where TSelf : struct, IRangeValueObject<TSelf>
{
}
