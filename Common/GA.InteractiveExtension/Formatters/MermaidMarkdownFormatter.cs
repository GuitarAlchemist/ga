namespace GA.InteractiveExtension.Formatters;

internal class MermaidMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters()
    {
        return this.CreateMermaidTypeFormatters();
    }
}
