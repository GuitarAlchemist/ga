using GA.InteractiveExtension.Formatters;

namespace GA.InteractiveExtension.Markdown;

[TypeFormatterSource(typeof(VexTabMarkdownFormatter))]
public class VexTabMarkDown
{
    private readonly string _value;

    internal string Width { get; set; }
    internal string Height { get; set; }

    public override string ToString() => _value;

    public VexTabMarkDown(string value)
    {
        Width = string.Empty;
        Height = string.Empty;
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }
}