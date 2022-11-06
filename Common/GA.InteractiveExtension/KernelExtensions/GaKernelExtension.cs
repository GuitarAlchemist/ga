using GA.Business.Core.Fretboard;

namespace GA.InteractiveExtension.KernelExtensions;

using System;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Formatting;

using Core.Extensions;
using Figgle;
using ExtensionMethods;

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
                    DisplayGaBanner(currentContext);
                    DisplayHelloWorld(currentContext);
                    DisplayMermaidExample(currentContext);
                    // DisplayTwoExample(currentContext);
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

    private static void DisplayGaBanner(KernelInvocationContext currentContext)
    {
        if (currentContext == null) throw new ArgumentNullException(nameof(currentContext));

        var banner = FiggleFonts.Standard.Render("Guitar Alchemist");
        // ReSharper disable StringLiteralTypo
        var html = $$""" 
<pre style="font-family: monospace">{{banner}}</pre>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    private static void DisplayAgGridExample(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        const string html = $$""" 
<div>
    Hello from ag-grid
    <div id="myGrid" style="height: 200px; width:500px;" class="ag-theme-balham"></div>
</div>

<script src="https://unpkg.com/ag-grid-community/dist/ag-grid-community.min.js"></script>
<script>
   var columnDefs = [
     {headerName: "Make", field: "make"},
     {headerName: "Model", field: "model"},
     {headerName: "Price", field: "price"}
   ];

   // specify the data
   var rowData = [
     {make: "Toyota", model: "Celica", price: 35000},
     {make: "Ford", model: "Mondeo", price: 32000},
     {make: "Porsche", model: "Boxter", price: 72000}
   ];

   // let the grid know which columns and what data to use
   var gridOptions = {
     columnDefs: columnDefs,
     rowData: rowData
   };

   // setup the grid after the page has finished loading
   document.addEventListener('DOMContentLoaded', function() {
       var gridDiv = document.querySelector('#myGrid');
       new agGrid.Grid(gridDiv, gridOptions);
   });
</script>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    private static void DisplayHelloWorld(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        const string html = $$""" 
<div>
    Hello World
</div>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    // TODO: Fix HTML rendering
    private static void DisplayFretboard(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        var html = $$""" 
<div>
    {{Fretboard.Default}}
</div>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    private static void DisplayTwoExample(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        const string html = $$""" 
<script type="module">
// Make an instance of two and place it on the page.
var params = {
  fullscreen: true
};
var elem = document.body;
var two = new Two(params).appendTo(elem);

// Two.js has convenient methods to make shapes and insert them into the scene.
var radius = 50;
var x = two.width * 0.5;
var y = two.height * 0.5 - radius * 1.25;
var circle = two.makeCircle(x, y, radius);

y = two.height * 0.5 + radius * 1.25;
var width = 100;
var height = 100;
var rect = two.makeRectangle(x, y, width, height);

// The object returned has many stylable properties:
circle.fill = '#FF8000';
// And accepts all valid CSS color:
circle.stroke = 'orangered';
circle.linewidth = 5;

rect.fill = 'rgb(0, 200, 255)';
rect.opacity = 0.75;
rect.noStroke();

// Don’t forget to tell two to draw everything to the screen
two.update();
</script>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    private static void DisplayMermaidExample(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        const string html = $$""" 
<pre class="mermaid">
            graph TD 
            A[Client] --> B[Load Balancer] 
            B --> C[Server1] 
            B --> D[Server3]
    </pre>

<script type="module">
      import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@9/dist/mermaid.esm.min.mjs';
      mermaid.initialize({ startOnLoad: true });
</script>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }
}