using GA.InteractiveExtension.Formatters;

namespace GA.InteractiveExtension.Markdown;

[TypeFormatterSource(typeof(VexFlowMarkdownFormatter))]
public class VexFlowMarkDown(string value)
{
    private readonly string _value = value ?? throw new ArgumentNullException(nameof(value));

    internal string Width { get; set; } = string.Empty;
    internal string Height { get; set; } = string.Empty;

    public override string ToString() => _value;
}