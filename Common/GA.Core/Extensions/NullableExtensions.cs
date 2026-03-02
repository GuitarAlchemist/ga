namespace GA.Core.Extensions;

public static class NullableExtensions
{
    extension<T>(T? obj) where T : struct
    {
        public bool TryGetValue(out T value)
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
}
