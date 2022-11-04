namespace GA.InteractiveExtension;

public class MarkdownHtmlGeneratorConfig
{
    public MarkdownHtmlGeneratorConfig(
        string libName, 
        string libVersion, 
        string libUrl,
        string libRenderJs)
    {
        LibName = libName;
        LibVersion = libVersion;
        LibUrl = libUrl;
        LibRenderJs = libRenderJs;
    }

    public string LibName { get; }
    public string LibVersion { get; }
    public string LibUrl { get; }
    public string LibRenderJs { get; }
    public string ElementId { get; init; } = $"{Guid.NewGuid():N}";
    public Guid CacheBusterGuid { get; init; } = Guid.NewGuid();
    public string RequireJsUri { get; init; } = "https://cdnjs.cloudflare.com/ajax/libs/require.js/2.3.6/require.min.js";
    public string? ContainerClass { get; init; }
    public string? ContainerStyle { get; init; }
}