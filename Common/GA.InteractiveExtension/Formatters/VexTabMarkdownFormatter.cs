namespace GA.InteractiveExtension.Formatters;

internal class VexTabMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters() => this.CreateVexTabTypeFormatters();
}
