namespace GA.Core.Extensions;

/// <summary>
///     Extension methods for <see cref="string" />.
/// </summary>
public static class StringExtensions
{
    extension(string s)
    {
        public string Coalesce(Func<string> stringProvider)
        {
            if (!string.IsNullOrEmpty(s))
            {
                return s;
            }

            var result = stringProvider();

            return result;
        }
    }
}
