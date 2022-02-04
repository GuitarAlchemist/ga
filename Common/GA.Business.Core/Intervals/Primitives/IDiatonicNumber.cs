namespace GA.Business.Core.Intervals.Primitives;

public interface IDiatonicNumber : IReadOnlyValue, IIsPerfect
{
}

[PublicAPI]
public interface IDiatonicNumber<TSelf> : IDiatonicNumber, IValue<TSelf>, IAll<TSelf>
    where TSelf : struct, IDiatonicNumber<TSelf>
{
}