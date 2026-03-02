namespace GA.InteractiveExtension.Formatters;

internal class MermaidMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters() => this.CreateMermaidTypeFormatters();
}
