namespace GA.InteractiveExtension;

using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Formatting;

using Core.Extensions;
using GA.Business.Core.Notes;

public static class GaExtensions
{
    public static async Task<int> UseGaAsync<T>([NotNull] this T kernel)
        where T : Kernel
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));

        var count = await Task.Run(() => RegisterFormatters(typeof(Note).Assembly));
        return count;
    }

    private static int RegisterFormatters(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        var types = assembly.MethodOverrideTypes("toString");
        foreach (var type in types)
        {
            Formatter.Register(type, (value, textWriter) => textWriter.Write(value?.ToString()));
        }

        return types.Count;
    }
}

