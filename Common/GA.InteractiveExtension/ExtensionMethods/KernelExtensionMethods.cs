using GA.InteractiveExtension.Ga;

namespace GA.InteractiveExtension.ExtensionMethods;

using GA.Business.Core.Notes;
using static PocketViewTags;

public static class KernelExtensionMethods
{
    public static async Task<ImmutableList<Type>> UseGaAsync<T>([NotNull] this T kernel)
        where T : Kernel
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));

        var types = typeof(Note).Assembly.RegisterFormatters();

        RegisterUriFormatter();

        await Task.CompletedTask;

        return types;
    }

    private static void RegisterUriFormatter()
    {
        Formatter.Register(
            typeof(Uri),
            (value, textWriter) =>
            {
                var sUri = ((Uri)value).ToString();
                textWriter.Write((IHtmlContent)a[href: sUri](
                    span(sUri)
                ));
            },
            HtmlFormatter.MimeType);
    }
}

