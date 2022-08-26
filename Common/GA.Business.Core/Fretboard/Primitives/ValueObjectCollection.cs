namespace GA.Business.Core.Fretboard.Primitives;

using System.Collections.Immutable;

public class ValueObjectCollection<TSelf> : IValueObjectCollection<TSelf> 
    where TSelf : struct, IValueObject<TSelf>
{
    public static IReadOnlyCollection<TSelf> Items => ValueObjectUtils<TSelf>.GetCollection();
    public static IReadOnlyCollection<int> Values => Items.Select(obj => obj.Value).ToImmutableList();
}