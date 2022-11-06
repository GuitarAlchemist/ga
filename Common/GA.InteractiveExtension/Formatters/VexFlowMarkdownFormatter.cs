namespace GA.InteractiveExtension.Formatters;

internal class VexFlowMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters() => this.CreateVexFlowTypeFormatters();
}