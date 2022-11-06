namespace GA.InteractiveExtension.Formatters;

using Microsoft.DotNet.Interactive.Formatting;

internal class VexTabMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters() => this.CreateVexTabTypeFormatters();
}