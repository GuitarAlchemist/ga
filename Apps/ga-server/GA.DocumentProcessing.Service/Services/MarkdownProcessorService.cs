namespace GA.DocumentProcessing.Service.Services;

using Markdig;

/// <summary>
/// Service for processing Markdown documents
/// </summary>
public class MarkdownProcessorService
{
    private readonly ILogger<MarkdownProcessorService> _logger;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownProcessorService(ILogger<MarkdownProcessorService> logger)
    {
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    /// <summary>
    /// Extract text from Markdown content
    /// </summary>
    public async Task<string> ExtractTextAsync(string markdownContent)
    {
        try
        {
            // Convert markdown to plain text (strip formatting)
            var html = Markdown.ToHtml(markdownContent, _pipeline);

            // Simple HTML tag removal (for plain text extraction)
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            text = System.Net.WebUtility.HtmlDecode(text);

            _logger.LogInformation("Extracted {CharCount} characters from markdown", text.Length);

            return await Task.FromResult(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from Markdown");
            throw new InvalidOperationException("Failed to extract text from Markdown", ex);
        }
    }

    /// <summary>
    /// Extract text from Markdown file
    /// </summary>
    public async Task<string> ExtractTextFromFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return await ExtractTextAsync(content);
    }

    /// <summary>
    /// Extract text from Markdown stream
    /// </summary>
    public async Task<string> ExtractTextFromStreamAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return await ExtractTextAsync(content);
    }
}

