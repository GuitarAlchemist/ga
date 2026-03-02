namespace GA.InteractiveExtension.ExtensionMethods;

using Domain.Core.Primitives.Notes;
using Ga;
using static PocketViewTags;

public static class KernelExtensionMethods
{
    private static void RegisterUriFormatter() =>
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

    public static async Task<ImmutableList<Type>> UseGaAsync<T>(this T kernel) where T : Kernel
    {
        ArgumentNullException.ThrowIfNull(kernel);

        var types = typeof(Note).Assembly.RegisterFormatters();

        RegisterUriFormatter();

        await Task.CompletedTask;

        return types;
    }
}
