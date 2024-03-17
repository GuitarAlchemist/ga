namespace GA.InteractiveExtension.Markdown;

using Formatters;

// Copyright (c) .NET Foundation and contributors. Items rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Interactive.Formatting;

[TypeFormatterSource(typeof(MermaidMarkdownFormatter))]
public class MermaidMarkdown(string value)
{
    internal string Background { get; set; } = "white";
    internal string Width { get; set; } = string.Empty;
    internal string Height { get; set; } = string.Empty;

    public override string ToString()
    {
        return _value;
    }

    private readonly string _value = value ?? throw new ArgumentNullException(nameof(value));
}