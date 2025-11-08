namespace GA.InteractiveExtension.Formatters;

internal class VexFlowMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters()
    {
        return this.CreateVexFlowTypeFormatters();
    }
}
