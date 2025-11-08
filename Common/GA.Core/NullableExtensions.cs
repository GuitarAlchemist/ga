namespace GA.Core;

public static class NullableExtensions
{
    public static bool TryGetValue<T>(this T? obj, out T value)
        where T : struct
    {
        value = default;
        if (!obj.HasValue)
        {
            return false;
        }

        value = obj.Value;
        return true;
    }
}
