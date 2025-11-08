namespace GA.InteractiveExtension.Formatters;

internal class VexTabMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters()
    {
        return this.CreateVexTabTypeFormatters();
    }
}
