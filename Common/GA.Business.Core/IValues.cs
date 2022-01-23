namespace GA.Business.Core;

[PublicAPI]
public interface IAll<out TSelf>
    where TSelf : struct, IValue<TSelf>
{
    public static abstract IReadOnlyCollection<TSelf> All { get; }
}