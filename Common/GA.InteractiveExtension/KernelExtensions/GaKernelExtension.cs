namespace GA.InteractiveExtension.KernelExtensions;

using System;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Formatting;

using Core.Extensions;
using Figgle;
using GA.InteractiveExtension.ExtensionMethods;

public class GaKernelExtension : IKernelExtension, IStaticContentSource
{
    public string Name => "GA";

    public async Task OnLoadAsync(Kernel kernel)
    {
        try
        {
            {
                if (KernelInvocationContext.Current is { } currentContext)
                {
                    var banner = FiggleFonts.Standard.Render("Guitar Alchemist");
                    // ReSharper disable StringLiteralTypo
                    var html = $$""" 
<pre style="font-family: monospace">{{banner}}</pre>
""";
                    // ReSharper restore StringLiteralTypo

                    currentContext.DisplayAs(html, HtmlFormatter.MimeType);
                }
            }

            var registeredTypes = await kernel.UseGaAsync();
            // kernel.UseVexTab();

            {
                if (KernelInvocationContext.Current is { } currentContext)
                {
                    var sTypes = string.Join(@"", registeredTypes.Select(type => $"<p>{type.FullName}</p>"));
                    var html = $$""" 
<details>
    <summary>Renders Guitar Alchemist objects in dotnet-interactive notebooks ({{registeredTypes.Count}} formatters registered).</summary>
    {{sTypes}}
</details>    
""";

                    currentContext.DisplayAs(html, HtmlFormatter.MimeType);
                }
            }


            /*
            var tuningOption = new Option<Tuning>(new[] { "-t", "--tuning" }, "The fretboard tuning");

            var fretboardCommand = new Command("#!fretboard", "Displays a fretboard")
            {
                tuningOption
            };

            fretboardCommand.SetHandler(
                tuning => KernelInvocationContext.Current.Display(tuning.DrawFretboard()), 
                tuningOption);

            kernel.AddDirective(fretboardCommand);

            {
                if (KernelInvocationContext.Current is { } currentContext)
                {
                    currentContext.DisplayAs(
                        "Added magic command `#!fretboard`.  Please see `#!fretboard --help for more information.`",
                        HtmlFormatter.MimeType);
                }
            }
            */


            //var vexTabCommand = new Command("#!vexTab", "Displays a VexTab")
            //{
            //    Handler = CommandHandler.Create(() => KernelInvocationContext.Current.Display(VexTabFormatter.DrawVexTab()))
            //};

            // var keyOption = new Option<Tuning>(new[] { "-t", "--key" }, "The key");

        }
        catch (Exception ex)
        {
            var msg = ex.GetMessageAndStackTrace($"Failed loading GA kernel extension - {ex.Message}");
            await kernel.SendAsync(new DisplayError(msg));
            throw;
        }
    }
}