namespace GA.Business.Web.Services;

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

/// <summary>
///     Service for fetching and extracting content from web pages
/// </summary>
public class WebScrapingService(
    HttpClient httpClient,
    WebContentCache cache,
    ILogger<WebScrapingService> logger)
{
    private static readonly HashSet<string> _allowedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "musictheory.net",
        "wikipedia.org",
        "en.wikipedia.org",
        "guitarnoise.com",
        "justinguitar.com",
        "ultimate-guitar.com",
        "songsterr.com",
        "teoria.com"
    };

    /// <summary>
    ///     Fetch and extract text content from a web page
    /// </summary>
    public async Task<string> FetchWebPageAsync(string url, bool extractMainContent = true)
    {
        try
        {
            if (!IsAllowedDomain(url))
            {
                return
                    $"Error: Domain not allowed. Only these domains are permitted: {string.Join(", ", _allowedDomains)}";
            }

            logger.LogInformation("Fetching web page: {Url}", url);

            var cacheKey = $"webpage:{url}:{extractMainContent}";
            var content = await cache.GetOrCreateAsync(
                cacheKey,
                async () => await FetchAndParseAsync(url, extractMainContent),
                TimeSpan.FromHours(2));

            return content;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error fetching {Url}", url);
            return $"Error fetching page: {ex.Message}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing {Url}", url);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    ///     Extract specific elements from a web page using CSS selectors
    /// </summary>
    public async Task<string> ExtractElementsAsync(string url, string cssSelector)
    {
        try
        {
            if (!IsAllowedDomain(url))
            {
                return "Error: Domain not allowed";
            }

            logger.LogInformation("Extracting elements from {Url} with selector: {Selector}", url, cssSelector);

            var cacheKey = $"elements:{url}:{cssSelector}";
            var content = await cache.GetOrCreateAsync(
                cacheKey,
                async () => await ExtractElementsInternalAsync(url, cssSelector),
                TimeSpan.FromHours(2));

            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting elements from {Url}", url);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    ///     Extract all links from a web page
    /// </summary>
    public async Task<string> ExtractLinksAsync(string url, string? filterDomain = null)
    {
        try
        {
            if (!IsAllowedDomain(url))
            {
                return "Error: Domain not allowed";
            }

            logger.LogInformation("Extracting links from {Url}", url);

            var html = await httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            if (links == null || !links.Any())
            {
                return "No links found on this page.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"?? Links from {url}:");
            sb.AppendLine();

            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                var text = WebUtility.HtmlDecode(link.InnerText.Trim());

                if (string.IsNullOrEmpty(href))
                {
                    continue;
                }

                // Make absolute URL
                if (!href.StartsWith("http"))
                {
                    var baseUri = new Uri(url);
                    href = new Uri(baseUri, href).ToString();
                }

                // Filter by domain if specified
                if (!string.IsNullOrEmpty(filterDomain))
                {
                    var linkUri = new Uri(href);
                    if (!linkUri.Host.Contains(filterDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                sb.AppendLine($"� {text}");
                sb.AppendLine($"  {href}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting links from {Url}", url);
            return $"Error: {ex.Message}";
        }
    }

    private bool IsAllowedDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return _allowedDomains.Any(domain => uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                                                 uri.Host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> FetchAndParseAsync(string url, bool extractMainContent)
    {
        var html = await httpClient.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        if (extractMainContent)
        {
            // Remove unwanted elements
            RemoveNodes(doc, "//script");
            RemoveNodes(doc, "//style");
            RemoveNodes(doc, "//nav");
            RemoveNodes(doc, "//header");
            RemoveNodes(doc, "//footer");
            RemoveNodes(doc, "//aside");
            RemoveNodes(doc, "//*[contains(@class, 'advertisement')]");
            RemoveNodes(doc, "//*[contains(@class, 'sidebar')]");

            // Try to find main content
            var mainContent = doc.DocumentNode.SelectSingleNode("//main") ??
                              doc.DocumentNode.SelectSingleNode("//article") ??
                              doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'content')]") ??
                              doc.DocumentNode.SelectSingleNode("//body");

            if (mainContent != null)
            {
                var text = WebUtility.HtmlDecode(mainContent.InnerText);
                return CleanText(text);
            }
        }

        var fullText = WebUtility.HtmlDecode(doc.DocumentNode.InnerText);
        return CleanText(fullText);
    }

    private async Task<string> ExtractElementsInternalAsync(string url, string cssSelector)
    {
        var html = await httpClient.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Convert CSS selector to XPath (simple conversion)
        var xpath = ConvertCssSelectorToXPath(cssSelector);
        var nodes = doc.DocumentNode.SelectNodes(xpath);

        if (nodes == null || !nodes.Any())
        {
            return $"No elements found matching selector: {cssSelector}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"?? Elements matching '{cssSelector}' from {url}:");
        sb.AppendLine();

        foreach (var node in nodes)
        {
            var text = WebUtility.HtmlDecode(node.InnerText.Trim());
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine($"� {text}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private void RemoveNodes(HtmlDocument doc, string xpath)
    {
        var nodes = doc.DocumentNode.SelectNodes(xpath);
        if (nodes != null)
        {
            foreach (var node in nodes.ToList())
            {
                node.Remove();
            }
        }
    }

    private string CleanText(string text)
    {
        // Remove excessive whitespace
        text = Regex.Replace(text, @"\s+", " ");
        text = Regex.Replace(text, @"\n\s*\n", "\n\n");
        return text.Trim();
    }

    private string ConvertCssSelectorToXPath(string cssSelector)
    {
        // Simple CSS to XPath conversion
        if (cssSelector.StartsWith("."))
        {
            var className = cssSelector.Substring(1);
            return $"//*[contains(@class, '{className}')]";
        }

        if (cssSelector.StartsWith("#"))
        {
            var id = cssSelector.Substring(1);
            return $"//*[@id='{id}']";
        }

        return $"//{cssSelector}";
    }
}
