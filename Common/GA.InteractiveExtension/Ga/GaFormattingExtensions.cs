namespace GA.InteractiveExtension.Ga;

using GA.Core.Extensions;

public static class GaFormattingExtensions
{
    public static int RegisterFormatters(this Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        var types = assembly.MethodOverrideTypes("toString");
        foreach (var type in types)
        {
            Formatter.Register(
                type,
                (value, textWriter) => textWriter.Write(value?.ToString()));
        }

        return types.Count;
    }
}
