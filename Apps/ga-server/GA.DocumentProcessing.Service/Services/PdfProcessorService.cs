namespace GA.DocumentProcessing.Service.Services;

using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

/// <summary>
/// Service for processing PDF documents
/// </summary>
public class PdfProcessorService
{
    private readonly ILogger<PdfProcessorService> _logger;

    public PdfProcessorService(ILogger<PdfProcessorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extract text from PDF file
    /// </summary>
    public async Task<(string Text, int PageCount)> ExtractTextAsync(Stream pdfStream)
    {
        try
        {
            using var pdfReader = new PdfReader(pdfStream);
            using var pdfDocument = new PdfDocument(pdfReader);

            var pageCount = pdfDocument.GetNumberOfPages();
            var textBuilder = new System.Text.StringBuilder();

            for (int i = 1; i <= pageCount; i++)
            {
                var page = pdfDocument.GetPage(i);
                var strategy = new SimpleTextExtractionStrategy();
                var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

                textBuilder.AppendLine($"--- Page {i} ---");
                textBuilder.AppendLine(pageText);
                textBuilder.AppendLine();
            }

            var fullText = textBuilder.ToString();
            _logger.LogInformation("Extracted {CharCount} characters from {PageCount} pages",
                fullText.Length, pageCount);

            return await Task.FromResult((fullText, pageCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF");
            throw new InvalidOperationException("Failed to extract text from PDF", ex);
        }
    }

    /// <summary>
    /// Extract text from PDF file path
    /// </summary>
    public async Task<(string Text, int PageCount)> ExtractTextAsync(string pdfPath)
    {
        using var fileStream = File.OpenRead(pdfPath);
        return await ExtractTextAsync(fileStream);
    }
}

