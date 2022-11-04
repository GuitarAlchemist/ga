namespace GA.InteractiveExtension.VexTab;

internal class VexFlowMarkdownFormatter : ITypeFormatterSource
{
    public IEnumerable<ITypeFormatter> CreateTypeFormatters() => this.CreateVexFlowTypeFormatters();
}