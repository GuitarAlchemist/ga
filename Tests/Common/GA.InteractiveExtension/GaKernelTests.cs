using GA.InteractiveExtension.Markdown;

namespace GA.InteractiveExtension.Tests;

using Microsoft.DotNet.Interactive.Formatting;
using FluentAssertions;
using FluentAssertions.Execution;
using HtmlAgilityPack;
using Xunit;

public class GaKernelTests
{
    [Fact]
    public void registers_html_formatter_for_MermaidMarkdown()
    {
        const string markdown = @"
graph TD
    Am[Client] --> B[Load Balancer]
    B --> Cm[Server1]
    B --> Dm[Server2]";

        var formatted = new MermaidMarkdown(markdown).ToDisplayString(HtmlFormatter.MimeType);
        var doc = new HtmlDocument();
        doc.LoadHtml(formatted.FixedGuid().FixedCacheBuster());
        var scriptNode = doc.DocumentNode.SelectSingleNode("//div/script");
        var renderTarget = doc.DocumentNode.SelectSingleNode("//div[@id='00000000000000000000000000000000']");
        using var _ = new AssertionScope();

        scriptNode.Should().NotBeNull();
        scriptNode.InnerText.Should()
            .Contain(markdown);
        scriptNode.InnerText.Should()
            .Contain("(['mermaidUri'], (mermaid) => {");

        renderTarget.Should().NotBeNull();
    }

    [Fact]
    public void registers_html_formatter_for_VexTabMarkdown()
    {
        // ReSharper disable once StringLiteralTypo
        const string markdown = @"
tabstave notation=true time=4/4 key=Abm
tuning=eb
notes :8 5s7s8/5 ^3^ :q (5/2.6/3)h(7/3) :8d 5/4 :16 5/5";

        var formatted = new VexTabMarkDown(markdown).ToDisplayString(HtmlFormatter.MimeType);
        var doc = new HtmlDocument();
        doc.LoadHtml(formatted.FixedGuid().FixedCacheBuster());
        var scriptNode = doc.DocumentNode.SelectSingleNode("//div/script");
        var renderTarget = doc.DocumentNode.SelectSingleNode("//div[@id='00000000000000000000000000000000']");
        using var _ = new AssertionScope();

        scriptNode.Should().NotBeNull();
        scriptNode.InnerText.Should()
            .Contain(markdown);
        scriptNode.InnerText.Should()
            .Contain("(['mermaidUri'], (mermaid) => {");

        renderTarget.Should().NotBeNull();
    }
}
