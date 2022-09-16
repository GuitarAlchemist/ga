namespace GA.Business.Core;

[PublicAPI]
public interface IValueObjectCollection<out TSelf>
    where TSelf : struct, IValueObject<TSelf>
{
    public static abstract IReadOnlyCollection<TSelf> Items { get; }
    public static abstract IReadOnlyCollection<int> Values { get; }
}