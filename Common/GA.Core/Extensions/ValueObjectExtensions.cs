namespace GA.Core.Extensions;

[PublicAPI]
public static class ValueObjectExtensions
{
    extension<T>(IEnumerable<T> items) where T : IRangeValueObject<T>, new()
    {
        public ImmutableArray<int> ToValueArray()
        {
            ArgumentNullException.ThrowIfNull(items);

            return [..items.Select(item => item.Value)];
        }

        public ImmutableList<int> ToValueList()
        {
            ArgumentNullException.ThrowIfNull(items);

            return [.. items.Select(item => item.Value)];
        }
    }
}
