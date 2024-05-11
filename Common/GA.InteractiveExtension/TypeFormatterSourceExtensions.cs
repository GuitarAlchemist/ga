namespace GA.InteractiveExtension;

using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Markdown;

public static partial class TypeFormatterSourceExtensions
{
    private static readonly Regex _markdownEscapeRegex = MyRegex();

    /*
    private const string DefaultLibraryVersion = "9.1.7";
    private static readonly Uri DefaultLibraryUri = new($@"https://cdn.jsdelivr.net/npm/mermaid@{DefaultLibraryVersion}/dist/mermaid.min.js", UriKind.Absolute);
    private static readonly Uri RequireUri = new("https://cdnjs.cloudflare.com/ajax/libs/require.js/2.3.6/require.min.js");
     */

    private static readonly Func<MarkdownHtmlGeneratorConfig> _mermaidConfigFactory = () =>
        new("mermaid",  
            "9.1.7", 
            "https://cdn.jsdelivr.net/npm/mermaid@9.1.7/dist/mermaid.min", 
            "(lib) => lib.mermaidAPI.render(`${libName}_${id}`, libMarkup, g => renderTarget.innerHTML = g)");

    public static IEnumerable<HtmlFormatter<TMarkdown>> CreateMarkdownLibTypeFormatters<TMarkdown>(
        Func<MarkdownHtmlGeneratorConfig> configFactory,
        ITypeFormatterSource typeFormatterSource)
    {
        yield return new((markDown, context) =>
        {
            var html = GenerateMarkdownHtml(configFactory(), markDown?.ToString() ?? string.Empty);
            html.WriteTo(context.Writer, HtmlEncoder.Default);
        });
    }

    public static IEnumerable<HtmlFormatter<VexTabMarkDown>> CreateVexTabTypeFormatters(this ITypeFormatterSource typeFormatterSource)
    {
        yield return new((markDown, context) =>
        {
            var html = GenerateVexTabHtml(markDown?.ToString() ?? string.Empty);
            html.WriteTo(context.Writer, HtmlEncoder.Default);
        });
    }

    public static IEnumerable<HtmlFormatter<VexTabMarkDown>> CreateVexFlowTypeFormatters(this ITypeFormatterSource typeFormatterSource)
    {
        yield return new((markDown, context) =>
        {
            var html = GenerateVexFlowHtml(markDown?.ToString() ?? string.Empty);
            html.WriteTo(context.Writer, HtmlEncoder.Default);
        });
    }

    public static IEnumerable<HtmlFormatter<MermaidMarkdown>> CreateMermaidTypeFormatters(this ITypeFormatterSource typeFormatterSource) 
        => CreateMarkdownLibTypeFormatters<MermaidMarkdown>(_mermaidConfigFactory, typeFormatterSource);

    public static IHtmlContent GenerateMarkdownHtml(
        MarkdownHtmlGeneratorConfig config,
        string markDown)
    {
        ArgumentNullException.ThrowIfNull(config);

        var code = $$""" 
<script type="text/javascript">
    const cacheBuster = 'cacheBuster={{config.CacheBusterGuid:N}}';
    const id = '{{config.ElementId}}';
    const libName = '{{config.LibName}}';
    const libVersion = '{{config.LibVersion}}';
    const libUri = '{{config.LibUrl}}';
    const libMarkup = `{{MarkdownEscape(markDown)}}`;

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
            requireScript.setAttribute('src', '{{config.RequireJsUri}}');
            requireScript.setAttribute('type', 'text/javascript');

            requireScript.onload = onload;
            document.getElementsByTagName('head')[0].appendChild(requireScript);
        }

        return result;
    };

    const libRender = {{config.LibRenderJs}};

    const loadAndRender = () => {
        (window.require.config(requireJsConfig) || window.require)(
            ['libUri'],
            (lib) => { libRender(lib) },
            (error) => console.log(error));
    };

    if (!installRequireScript(loadAndRender)) loadAndRender();
</script>

<div id="{{config.ElementId}}"></div>
""";

        return new HtmlString(code);

        static string MarkdownEscape(string markdown) => _markdownEscapeRegex.Replace(markdown, @"${pre}\\n");
    }

    private static IHtmlContent GenerateVexTabHtml(string markdown)
    {
        var code = $$"""
<div id="boo"></div>

<script type="text/javascript" src="https://unpkg.com/vextab/releases/main.dev.js"></script>

<script type="text/javascript">
    let data = `
{{markdown}}
`;
    const VF = vextab.Vex.Flow;
    const renderer = new VF.Renderer($('#boo')[0], VF.Renderer.Backends.SVG);
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

        return new HtmlString(code);
    }

    private static IHtmlContent GenerateVexFlowHtml(string markdown)
    {
        var code = $$"""
<div id="output"></div>

<script src="https://cdn.jsdelivr.net/npm/vexflow@4.0.3/build/cjs/vexflow.js"></script>
<script>
    const {
        Factory,
        EasyScore,
        System
    } = Vex.Flow;
    const vf = new Factory({
        renderer: {
            elementId: 'output',
            width: 500,
            height: 200
        },
    });
    const score = vf.EasyScore();
    const system = vf.System();
    system
        .addStave({
            voices: [
                score.voice(score.notes('Cm#5/q, B4, A4, Gm#4', {
                    stem: 'up'
                })),
                score.voice(score.notes('Cm#4/h, Cm#4', {
                    stem: 'down'
                })),
            ],
        })
        .addClef('treble')
        .addTimeSignature('4/4');
    vf.draw();
</script>
""";
        return new HtmlString(code);
    }

    [GeneratedRegex(@"(?<pre>[^\\])(?<newLine>\\n)", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}

