namespace GA.InteractiveExtension;

public class MarkdownHtmlGeneratorConfig(string libName, 
    string libVersion, 
    string libUrl,
    string libRenderJs)
{
    public string LibName { get; } = libName;
    public string LibVersion { get; } = libVersion;
    public string LibUrl { get; } = libUrl;
    public string LibRenderJs { get; } = libRenderJs;
    public string ElementId { get; init; } = $"{Guid.NewGuid():N}";
    public Guid CacheBusterGuid { get; init; } = Guid.NewGuid();
    public string RequireJsUri { get; init; } = "https://cdnjs.cloudflare.com/ajax/libs/require.js/2.3.6/require.min.js";
    public string? ContainerClass { get; init; }
    public string? ContainerStyle { get; init; }
}