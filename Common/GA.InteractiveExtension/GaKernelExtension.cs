namespace GA.InteractiveExtension;

using Microsoft.AspNetCore.Html;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Formatting;

public class GaKernelExtension : IKernelExtension, IStaticContentSource
{
    public string Name => "GA";

    public async Task OnLoadAsync(Kernel kernel)
    {
        //if (kernel is CompositeKernel compositeKernel)
        //{
        //    compositeKernel.Add(new GaKernel());
        //}

        var count = await kernel.UseGaAsync();

        var message = new HtmlString(@$"
<details>
    <summary>Renders Guitar Alchemist objects in dotnet-interactive notebooks ({count} formatter registered).</summary>
</details>");

        var formattedValue = new FormattedValue(
            HtmlFormatter.MimeType,
            message.ToDisplayString(HtmlFormatter.MimeType));

        await kernel.SendAsync(new DisplayValue(formattedValue, Guid.NewGuid().ToString()));
    }
}