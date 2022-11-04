namespace GA.InteractiveExtension.VexTab;

using Microsoft.DotNet.Interactive.Formatting;

internal class MermaidMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters() => this.CreateMermaidTypeFormatters();
}