namespace GA.TabConversion.Api.Services;

using System.Diagnostics;
using Models;
using MusicTheory.DSL.Parsers;
using MusicTheory.DSL.Types;

/// <summary>
///     Implementation of tab conversion service
/// </summary>
public class TabConversionService : ITabConversionService
{
    // Format detection patterns
    private const string VexTabPattern1 = "tabstave";
    private const string VexTabPattern2 = "notes :";
    private const string AsciiTabPattern1 = "|";
    private const string AsciiTabPattern2 = "E|";
    private const string AsciiTabPattern3 = "e|";

    // Supported formats list (cached, immutable)
    private static readonly IReadOnlyList<FormatInfo> SupportedFormats = new List<FormatInfo>
    {
        new()
        {
            Id = "ascii",
            Name = "AsciiTab",
            Extensions = [".txt", ".tab"],
            Description = "Plain text guitar tablature",
            SupportsRead = true,
            SupportsWrite = true,
            Category = "text"
        },
        new()
        {
            Id = "vextab",
            Name = "VexTab",
            Extensions = [".vextab"],
            Description = "VexFlow tablature notation",
            SupportsRead = true,
            SupportsWrite = true,
            Category = "text"
        },
        new()
        {
            Id = "midi",
            Name = "MIDI",
            Extensions = [".mid", ".midi"],
            Description = "Musical Instrument Digital Interface",
            SupportsRead = true,
            SupportsWrite = true,
            Category = "binary"
        },
        new()
        {
            Id = "musicxml",
            Name = "MusicXML",
            Extensions = [".musicxml", ".xml", ".mxl"],
            Description = "Universal music notation format",
            SupportsRead = true,
            SupportsWrite = true,
            Category = "xml"
        },
        new()
        {
            Id = "gp",
            Name = "Guitar Pro",
            Extensions = [".gp3", ".gp4", ".gp5", ".gpx", ".gp7"],
            Description = "Guitar Pro tablature files",
            SupportsRead = true,
            SupportsWrite = false,
            Category = "binary"
        }
    }.AsReadOnly();

    private readonly ILogger<TabConversionService> _logger;

    public TabConversionService(ILogger<TabConversionService> logger)
    {
        _logger = logger;
    }

    public async Task<ConversionResponse> ConvertAsync(ConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Content);

        var sw = Stopwatch.StartNew();
        var response = new ConversionResponse();

        try
        {
            _logger.LogInformation("Converting from {Source} to {Target}", request.SourceFormat, request.TargetFormat);

            // Validate formats
            if (!IsFormatSupported(request.SourceFormat))
            {
                response.Success = false;
                response.Errors.Add($"Unsupported source format: {request.SourceFormat}");
                return response;
            }

            if (!IsFormatSupported(request.TargetFormat))
            {
                response.Success = false;
                response.Errors.Add($"Unsupported target format: {request.TargetFormat}");
                return response;
            }

            // Same format - just return content
            if (request.SourceFormat.Equals(request.TargetFormat, StringComparison.OrdinalIgnoreCase))
            {
                response.Success = true;
                response.Result = request.Content;
                response.Warnings.Add("Source and target formats are the same - no conversion needed");
                return response;
            }

            // Perform conversion
            var result = await PerformConversionAsync(request, cancellationToken).ConfigureAwait(false);

            response.Success = result.Success;
            response.Result = result.Result;
            response.Errors = result.Errors;
            response.Warnings = result.Warnings;

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during conversion from {Source} to {Target}", request.SourceFormat,
                request.TargetFormat);
            response.Success = false;
            response.Errors.Add($"Conversion failed: {ex.Message}");
            return response;
        }
        finally
        {
            sw.Stop();
            response.Metadata = new ConversionMetadata
            {
                DurationMs = sw.ElapsedMilliseconds,
                DetectedSourceFormat = request.SourceFormat
            };
        }
    }

    public async Task<ValidationResponse> ValidateAsync(ValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Format);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Content);

        var response = new ValidationResponse();

        try
        {
            _logger.LogInformation("Validating {Format} content", request.Format);

            if (!IsFormatSupported(request.Format))
            {
                response.IsValid = false;
                response.Errors.Add($"Unsupported format: {request.Format}");
                return response;
            }

            // Validate based on format (run in background to support cancellation)
            var validationResult = await Task
                .Run(() => ValidateFormat(request.Format, request.Content), cancellationToken).ConfigureAwait(false);

            response.IsValid = validationResult.IsValid;
            response.Errors = validationResult.Errors;
            response.Warnings = validationResult.Warnings;
            response.DetectedFormat = validationResult.DetectedFormat;

            _logger.LogInformation("Validation completed for {Format}: {IsValid}", request.Format, response.IsValid);
            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Validation cancelled for {Format}", request.Format);
            response.IsValid = false;
            response.Errors.Add("Validation was cancelled");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation of {Format}", request.Format);
            response.IsValid = false;
            response.Errors.Add($"Validation failed: {ex.Message}");
            return response;
        }
    }

    public Task<FormatsResponse> GetFormatsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FormatsResponse
        {
            Formats = SupportedFormats.ToList() // Return a copy to prevent external modification
        });
    }

    public Task<string?> DetectFormatAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        // Simple heuristics for format detection (case-insensitive)
        if (content.Contains(VexTabPattern1, StringComparison.OrdinalIgnoreCase) ||
            content.Contains(VexTabPattern2, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<string?>("VexTab");
        }

        if (content.Contains(AsciiTabPattern1, StringComparison.Ordinal) &&
            (content.Contains(AsciiTabPattern2, StringComparison.Ordinal) ||
             content.Contains(AsciiTabPattern3, StringComparison.Ordinal)))
        {
            return Task.FromResult<string?>("AsciiTab");
        }

        // Default to AsciiTab for text content
        return Task.FromResult<string?>("AsciiTab");
    }

    /// <summary>
    ///     Perform the actual conversion between formats
    /// </summary>
    private async Task<ConversionResponse> PerformConversionAsync(ConversionRequest request,
        CancellationToken cancellationToken)
    {
        var response = new ConversionResponse();

        try
        {
            // Normalize format names
            var normalizedSource = NormalizeFormat(request.SourceFormat);
            var normalizedTarget = NormalizeFormat(request.TargetFormat);

            _logger.LogDebug("Normalized formats: {Source} -> {Target}", normalizedSource, normalizedTarget);

            // Run conversion in background to support cancellation
            return await Task.Run(() =>
            {
                // ASCII → VexTab
                if (normalizedSource == "ascii" && normalizedTarget == "vextab")
                {
                    return ConvertAsciiToVexTab(request.Content);
                }

                // VexTab → ASCII
                if (normalizedSource == "vextab" && normalizedTarget == "ascii")
                {
                    return ConvertVexTabToAscii(request.Content);
                }

                // Guitar Pro → ASCII
                if (normalizedSource == "gp" && normalizedTarget == "ascii")
                {
                    return ConvertGuitarProToAscii(request.Content);
                }

                // Not implemented yet
                response.Success = false;
                response.Errors.Add(
                    $"Conversion from {request.SourceFormat} to {request.TargetFormat} is not yet implemented");
                return response;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Conversion cancelled: {Source} to {Target}", request.SourceFormat,
                request.TargetFormat);
            response.Success = false;
            response.Errors.Add("Conversion was cancelled");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversion error: {Source} to {Target}", request.SourceFormat, request.TargetFormat);
            response.Success = false;
            response.Errors.Add($"Conversion error: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    ///     Convert ASCII tab format to VexTab notation
    /// </summary>
    private ConversionResponse ConvertAsciiToVexTab(string content)
    {
        var response = new ConversionResponse();

        try
        {
            _logger.LogDebug("Parsing ASCII tab for conversion to VexTab");

            // Parse ASCII tab
            var parseResult = AsciiTabParser.parse(content);

            if (parseResult.IsError)
            {
                _logger.LogWarning("Failed to parse ASCII tab: {Error}", parseResult.ErrorValue);
                response.Success = false;
                response.Errors.Add($"Failed to parse ASCII tab: {parseResult.ErrorValue}");
                return response;
            }

            // Note: Full conversion not yet implemented
            // This would require mapping ASCII tab measures/notes to VexTab notation
            _logger.LogWarning("ASCII to VexTab conversion not fully implemented");
            response.Success = false;
            response.Errors.Add("ASCII to VexTab conversion is not yet fully implemented");
            response.Warnings.Add("Parser successfully validated the ASCII tab format");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ASCII to VexTab conversion error");
            response.Success = false;
            response.Errors.Add($"ASCII to VexTab conversion error: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    ///     Convert VexTab notation to ASCII tab format
    /// </summary>
    private ConversionResponse ConvertVexTabToAscii(string content)
    {
        var response = new ConversionResponse();

        try
        {
            _logger.LogDebug("Parsing VexTab for conversion to ASCII");

            // Parse VexTab
            var parseResult = VexTabParser.parse(content);

            if (parseResult.IsError)
            {
                _logger.LogWarning("Failed to parse VexTab: {Error}", parseResult.ErrorValue);
                response.Success = false;
                response.Errors.Add($"Failed to parse VexTab: {parseResult.ErrorValue}");
                return response;
            }

            // Note: Full conversion not yet implemented
            // This would require mapping VexTab notation to ASCII tab format
            _logger.LogWarning("VexTab to ASCII conversion not fully implemented");
            response.Success = false;
            response.Errors.Add("VexTab to ASCII conversion is not yet fully implemented");
            response.Warnings.Add("Parser successfully validated the VexTab format");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VexTab to ASCII conversion error");
            response.Success = false;
            response.Errors.Add($"VexTab to ASCII conversion error: {ex.Message}");
            return response;
        }
    }


    /// <summary>
    ///     Convert Guitar Pro file to ASCII tab format
    /// </summary>
    private ConversionResponse ConvertGuitarProToAscii(string content)
    {
        var response = new ConversionResponse();

        try
        {
            _logger.LogDebug("Parsing Guitar Pro file for conversion to ASCII");

            // Parse Guitar Pro file (base64 encoded binary)
            var parseResult = GuitarProParser.parse(content);

            // Pattern match on the result
            if (parseResult is GuitarProTypes.GuitarProParseResult.Error errorResult)
            {
                _logger.LogWarning("Failed to parse Guitar Pro file: {Error}", errorResult.Item);
                response.Success = false;
                response.Errors.Add($"Failed to parse Guitar Pro file: {errorResult.Item}");
                return response;
            }

            // Extract document from Success case
            var gpDoc = ((GuitarProTypes.GuitarProParseResult.Success)parseResult).Item;

            // Convert to ASCII tab using the built-in converter
            var ascii = GuitarProParser.toAsciiTab(gpDoc);

            _logger.LogInformation("Successfully converted Guitar Pro to ASCII tab");
            response.Success = true;
            response.Result = ascii;
            response.Warnings.Add("Guitar Pro conversion is simplified - full measure/beat/note parsing in progress");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guitar Pro to ASCII conversion error");
            response.Success = false;
            response.Errors.Add($"Guitar Pro to ASCII conversion error: {ex.Message}");
            return response;
        }
    }


    /// <summary>
    ///     Validate content against a specific format
    /// </summary>
    private ValidationResponse ValidateFormat(string format, string content)
    {
        var response = new ValidationResponse();

        try
        {
            // Normalize format name
            var normalizedFormat = NormalizeFormat(format);

            switch (normalizedFormat)
            {
                case "ascii":
                    _logger.LogDebug("Validating ASCII tab format");
                    var asciiResult = AsciiTabParser.parse(content);
                    response.IsValid = asciiResult.IsOk;
                    if (asciiResult.IsError)
                    {
                        _logger.LogWarning("ASCII tab validation failed: {Error}", asciiResult.ErrorValue);
                        response.Errors.Add(asciiResult.ErrorValue);
                    }
                    else
                    {
                        _logger.LogDebug("ASCII tab validation succeeded");
                    }

                    break;

                case "vextab":
                    _logger.LogDebug("Validating VexTab format");
                    var vexTabResult = VexTabParser.parse(content);
                    response.IsValid = vexTabResult.IsOk;
                    if (vexTabResult.IsError)
                    {
                        _logger.LogWarning("VexTab validation failed: {Error}", vexTabResult.ErrorValue);
                        response.Errors.Add(vexTabResult.ErrorValue);
                    }
                    else
                    {
                        _logger.LogDebug("VexTab validation succeeded");
                    }

                    break;

                case "gp":
                    _logger.LogDebug("Validating Guitar Pro format");
                    var gpResult = GuitarProParser.parse(content);
                    if (gpResult is GuitarProTypes.GuitarProParseResult.Error errorResult)
                    {
                        response.IsValid = false;
                        response.Errors.Add(errorResult.Item);
                        _logger.LogWarning("Guitar Pro validation failed: {Error}", errorResult.Item);
                    }
                    else
                    {
                        response.IsValid = true;
                        _logger.LogDebug("Guitar Pro validation succeeded");
                    }

                    break;

                case "midi":
                    _logger.LogDebug("MIDI validation not yet implemented");
                    response.IsValid = false;
                    response.Errors.Add("MIDI validation not yet implemented");
                    break;

                case "musicxml":
                    _logger.LogDebug("MusicXML validation not yet implemented");
                    response.IsValid = false;
                    response.Errors.Add("MusicXML validation not yet implemented");
                    break;

                default:
                    _logger.LogWarning("Validation not implemented for format: {Format}", format);
                    response.IsValid = false;
                    response.Errors.Add($"Validation not implemented for format: {format}");
                    break;
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation error for format {Format}", format);
            response.IsValid = false;
            response.Errors.Add($"Validation error: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    ///     Check if a format is supported by the service
    /// </summary>
    private bool IsFormatSupported(string format)
    {
        var normalizedFormat = NormalizeFormat(format);
        return SupportedFormats.Any(f => f.Id.Equals(normalizedFormat, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Normalize format name to standard ID
    /// </summary>
    private static string NormalizeFormat(string format)
    {
        var normalized = format.ToLowerInvariant();
        return normalized switch
        {
            "asciitab" => "ascii",
            "guitarpro" => "gp",
            _ => normalized
        };
    }
}
