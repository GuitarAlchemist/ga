namespace GA.InteractiveExtension.Ga;

using GA.Business.Core.Notes;
using static PocketViewTags;

public static class KernelExtensions
{
    public static async Task<int> UseGaAsync<T>([NotNull] this T kernel)
        where T : Kernel
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));

        var count = 0;
        count += typeof(Note).Assembly.RegisterFormatters();

        RegisterUriFormatter();
        count++;

        await Task.CompletedTask;

        return count;
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

