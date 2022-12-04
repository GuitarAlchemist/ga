namespace GA.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    public static string Coalesce(
        this string s, 
        Func<string> stringProvider)
    {
        if (!string.IsNullOrEmpty(s)) return s;
        var result = stringProvider();

        return result;
    }
}