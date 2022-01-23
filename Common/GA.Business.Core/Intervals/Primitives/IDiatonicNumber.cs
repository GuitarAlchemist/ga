namespace GA.Business.Core.Intervals.Primitives;

[PublicAPI]
public interface IDiatonicNumber<TSelf> : IValue<TSelf>, IAll<TSelf>
    where TSelf : struct, IDiatonicNumber<TSelf>
{
}