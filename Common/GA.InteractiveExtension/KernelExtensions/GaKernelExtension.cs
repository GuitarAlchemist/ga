namespace GA.InteractiveExtension.KernelExtensions;

using System;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Formatting;

using GA.Business.Core.Fretboard;
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
                    // DisplayForceGraph(currentContext);
                    // DisplayHelloWorld(currentContext);

//                    DisplayMermaidExample1(currentContext, """
//    graph TD
//    Am[Client] --> B[Load Balancer]
//    B --> Cm[Server01]
//    B --> Dm[Server02]
//""");

                    DisplayForceGraph(currentContext);

//                    DisplayMermaidExample2(currentContext, 
//"""
//graph TD
//Am[Client] --> B[Load Balancer]
//B --> Cm[Server01]
//B --> Dm[Server02]
//""");

                    // DisplayVexFlow(currentContext);

                    // DisplayVerivio(currentContext);
                    //DisplayTwoExample(currentContext);
                    // DisplayHelloWorld(currentContext);

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
    <summary>
        Renders Guitar Alchemist objects in dotnet-interactive notebooks
        ({{registeredTypes.Count}} formatters registered)
    </summary>
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
        ArgumentNullException.ThrowIfNull(currentContext);

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
<div id="draw-shapes" style="height: 100px; width: 100px"></div>

<script type="module">
    import Two from 'https://cdn.skypack.dev/two.js@latest';

    var elem = document.getElementById('draw-shapes');
    var two = new Two({ fitted: true }).appendTo(elem);

    var circle = two.makeCircle(400, 250, 75);
    circle.fill = 'navy';
    circle.noStroke();

    let group = two.makeGroup(circle);

    two.update();
</script>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    private static void DisplayMermaidExample1(
        KernelInvocationContext currentContext, 
        string markup)
    {
        // ReSharper disable StringLiteralTypo
        var config = new
        {
            CacheBusterGuid = Guid.NewGuid(),
            Id = Guid.NewGuid(),
            LibVersion = "9.1.7"
        };

        var html = $$""" 
<script type="text/javascript">
    const cacheBuster = 'cacheBuster={{config.CacheBusterGuid:N}}';
    const id = '{{config.Id:N}}';
    const libName = 'mermaid';
    const libVersion = '{{config.LibVersion}}';
    const libUri = 'https://cdn.jsdelivr.net/npm/mermaid@{{config.LibVersion}}/dist/mermaid.min';
    const libMarkup = `{{markup}}`;

    const renderTarget = document.getElementById(id);

    const requireJsConfig = {
        'paths': {
            'context': libVersion,
            'libUri': libUri,
            'urlArgs': cacheBuster
        }
    };

    // ensure `require` is available globally
    function installRequireScript(onload) {
        const result = (typeof (window.require) !== typeof (Function)) || (typeof (window.require.config) !== typeof (Function));
        if ((typeof (window.require) !== typeof (Function)) || (typeof (window.require.config) !== typeof (Function))) {
            const requireScript = document.createElement('script');
            requireScript.setAttribute('src', 'https://cdnjs.cloudflare.com/ajax/libs/require.js/2.3.6/require.min.js');
            requireScript.setAttribute('type', 'text/javascript');

            requireScript.onload = onload;
            document.getElementsByTagName('head')[0].appendChild(requireScript);
        }

        return result;
    };

    const libRender = (lib) => lib.mermaidAPI.render(`${libName}_${id}`, libMarkup, g => renderTarget.innerHTML = g);

    const loadAndRender = () => {
        (window.require.config(requireJsConfig) || window.require)(
            ['libUri'],
            (lib) => { libRender(lib) },
            (error) => console.log(error));
    };

    if (!installRequireScript(loadAndRender)) loadAndRender();
</script>

<div id="{{config.Id:N}}" style="background-color: white"></div>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }


    private static void DisplayMermaidExample2(
        KernelInvocationContext currentContext,
        string markup)
    {
        // ReSharper disable StringLiteralTypo
        var html = $$""" 
<script type="module">
    import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@9/dist/mermaid.esm.min.mjs';
    mermaid.initialize({ startOnLoad: true });
</script>
    
<pre class="mermaid">
{{markup}}
</pre>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    private static void DisplayVexTab(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        var html = $$""" 
<div id="d480a0b0b6a85684650fb232a86f8b3c1" style="ba"></div>

<script type="text/javascript" src="https://unpkg.com/vextab/releases/main.dev.js"></script>

<script type="text/javascript">
    let data = `
options tab-stems=true tab-stem-direction=down
tabstave notation=true tablature=true time=4/4
notes :8 3/5 0-2-3/4 0-2/3 0-1-1-0/2 2-0/3 3-2-0/4 3/5
text :8,.1,Cm,Dm,Em,Fm,Gm,Am,B,Cm,Cm,B,Am,Gm,Fm,Em,Dm,Cm
`;
    const VF = vextab.Vex.Flow;
    const renderTarget = document.getElementById('d480a0b0b6a85684650fb232a86f8b3c1');
    const renderer = new VF.Renderer(renderTarget, VF.Renderer.Backends.SVG);
    vextab.Artist.NOLOGO = true;

    // Initialize VexTab artist and parser.
    const artist = new vextab.Artist(10, 10, 750, {
        scale: 0.8
    });

    const tab = new vextab.VexTab(artist);
    tab.parse(data);
    artist.render(renderer);
</script>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    private static void DisplayVexFlow(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        const string html = $$""" 
<div id="d480a0b0b6a85684650fb232a86f8b3c1" style="background-color: white"></div>

<script type="module">
    import { Vex} from "https://cdn.skypack.dev/vexflow";

    Vex.Flow.setMusicFont("Bravura");

    const factory = new Vex.Flow.Factory({
        renderer: {
            elementId: "d480a0b0b6a85684650fb232a86f8b3c1",
            width: 500,
            height: 130
        },
    });
    const score = factory.EasyScore();
    factory
        .System()
        .addStave({
            voices: [score.voice(score.notes("Cm#5/q, B4, A4, Gm#4", {
                stem: "up"
            })), score.voice(score.notes("Cm#4/h, Cm#4", {
                stem: "down"
            }))],
        })
        .addClef("treble")
        .addTimeSignature("4/4");
    factory.draw();
</script>
""" ;
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }

    private static void DisplayVerivio(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        var html = $$""" 
<div class="panel-body">
    <div id="app" class="panel" style="border: 1px solid lightgray; min-height: 800px;"></div>
</div>

<script type="module">
    import 'https://www.verovio.org/javascript/app/verovio-app.js';

    // Create the app - here with an empty option object
    const app = new Verovio.App(document.getElementById("app"), {});

    // Load a file (MEI or MusicXML)
    fetch("https://www.verovio.org/editor/brahms.mei")
        .then(function(response) {
            return response.text();
        })
        .then(function(text) {
            app.loadData(text);
        });
</script>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }


    private static void DisplayForceGraph(KernelInvocationContext currentContext)
    {
        // ReSharper disable StringLiteralTypo
        var html = $$""" 
<div id="graph" style="background: white"></div>

<script type="module">
    await import('https://unpkg.com/force-graph/dist/force-graph.min.js');

    const gData = {
      nodes: [...Array(9).keys()].map(i => ({ id: i })),
      links: [
        { source: 1, target: 4, curvature: 0 },
        { source: 1, target: 4, curvature: 0.5 },
        { source: 1, target: 4, curvature: -0.5 },
        { source: 5, target: 2, curvature: 0.3 },
        { source: 2, target: 5, curvature: 0.3 },
        { source: 0, target: 3, curvature: 0 },
        { source: 3, target: 3, curvature: 0.5 },
        { source: 0, target: 4, curvature: 0.2 },
        { source: 4, target: 5, curvature: 0.5 },
        { source: 5, target: 6, curvature: 0.7 },
        { source: 6, target: 7, curvature: 1 },
        { source: 7, target: 8, curvature: 2 },
        { source: 8, target: 0, curvature: 0.5 }
      ]
    };

    const Graph = ForceGraph()
      (document.getElementById('graph'))
        .linkDirectionalParticles(2)
        .linkCurvature('curvature')
        .graphData(gData);
</script>
""";
        // ReSharper restore StringLiteralTypo

        currentContext.DisplayAs(html, HtmlFormatter.MimeType);
    }
}