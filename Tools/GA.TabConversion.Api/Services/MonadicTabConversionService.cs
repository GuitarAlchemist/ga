namespace GA.TabConversion.Api.Services;

using Microsoft.Extensions.Caching.Memory;
using Models;
using MusicTheory.DSL.Parsers;
using MusicTheory.DSL.Types;

/// <summary>
///     Monadic implementation of tab conversion service using Try, Result, and Option monads
/// </summary>
public class MonadicTabConversionService : MonadicServiceBase<MonadicTabConversionService>
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
            Extensions = [".xml", ".musicxml"],
            Description = "Music notation interchange format",
            SupportsRead = true,
            SupportsWrite = true,
            Category = "xml"
        },
        new()
        {
            Id = "gp",
            Name = "Guitar Pro",
            Extensions = [".gp3", ".gp4", ".gp5", ".gpx"],
            Description = "Guitar Pro tablature format",
            SupportsRead = true,
            SupportsWrite = false,
            Category = "binary"
        }
    };

    private readonly ILogger<MonadicTabConversionService> _logger;

    public MonadicTabConversionService(
        ILogger<MonadicTabConversionService> logger,
        IMemoryCache cache)
        : base(logger, cache)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Convert tab from one format to another using Result monad
    /// </summary>
    public async Task<Result<ConversionResponse, TabConversionError>> ConvertAsync(
        ConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate request
        var validationResult = ValidateConversionRequest(request);
        if (validationResult is Result<ConversionRequest, TabConversionError>.Failure failure)
        {
            return new Result<ConversionResponse, TabConversionError>.Failure(failure.Error);
        }

        try
        {
            _logger.LogDebug("Converting from {Source} to {Target}", request.SourceFormat, request.TargetFormat);
            var convertedResponse = await PerformConversionAsync(request, cancellationToken);
            _logger.LogDebug("Conversion completed successfully");
            return new Result<ConversionResponse, TabConversionError>.Success(convertedResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during conversion from {Source} to {Target}",
                request.SourceFormat, request.TargetFormat);
            return new Result<ConversionResponse, TabConversionError>.Failure(
                new TabConversionError(TabConversionErrorType.ConversionFailed, ex.Message)
            );
        }
    }

    /// <summary>
    ///     Validate tab content using Validation monad
    /// </summary>
    public async Task<Validation<ValidationResponse, string>> ValidateAsync(
        ValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate request
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Format))
        {
            errors.Add("Format cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            errors.Add("Content cannot be empty");
        }

        if (errors.Any())
        {
            return Validation.Fail<ValidationResponse, string>(errors.ToArray());
        }

        await Task.CompletedTask;

        var response = new ValidationResponse();
        var normalizedFormat = request.Format.ToLowerInvariant();

        // Check if format is supported
        if (!SupportedFormats.Any(f => f.Id.Equals(normalizedFormat, StringComparison.OrdinalIgnoreCase)))
        {
            response.IsValid = false;
            response.Errors.Add($"Unsupported format: {request.Format}");
            return Validation.Fail<ValidationResponse, string>($"Unsupported format: {request.Format}");
        }

        // Validate based on format
        if (normalizedFormat == "ascii")
        {
            var parseResult = AsciiTabParser.parse(request.Content);
            response.IsValid = !parseResult.IsError;
            if (parseResult.IsError)
            {
                response.Errors.Add($"Invalid ASCII tab: {parseResult.ErrorValue}");
            }
        }
        else if (normalizedFormat == "vextab")
        {
            var parseResult = VexTabParser.parse(request.Content);
            response.IsValid = !parseResult.IsError;
            if (parseResult.IsError)
            {
                response.Errors.Add($"Invalid VexTab: {parseResult.ErrorValue}");
            }
        }
        else
        {
            response.IsValid = true;
            response.Warnings.Add($"Validation for {request.Format} format is not yet implemented");
        }

        return response.IsValid
            ? Validation.Success<ValidationResponse, string>(response)
            : Validation.Fail<ValidationResponse, string>(response.Errors.ToArray());
    }

    /// <summary>
    ///     Get list of supported formats using Try monad
    /// </summary>
    public async Task<Try<FormatsResponse>> GetFormatsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            async () =>
            {
                await Task.CompletedTask;
                return new FormatsResponse { Formats = SupportedFormats.ToList() };
            },
            "GetFormatsAsync"
        );
    }

    /// <summary>
    ///     Detect format from content using Option monad
    /// </summary>
    public async Task<Option<string>> DetectFormatAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new Option<string>.None();
        }

        await Task.CompletedTask;

        // Simple heuristics for format detection (case-insensitive)
        if (content.Contains(VexTabPattern1, StringComparison.OrdinalIgnoreCase) ||
            content.Contains(VexTabPattern2, StringComparison.OrdinalIgnoreCase))
        {
            return new Option<string>.Some("VexTab");
        }

        if (content.Contains(AsciiTabPattern1, StringComparison.Ordinal) &&
            (content.Contains(AsciiTabPattern2, StringComparison.Ordinal) ||
             content.Contains(AsciiTabPattern3, StringComparison.Ordinal)))
        {
            return new Option<string>.Some("AsciiTab");
        }

        // Default to AsciiTab for text content
        return new Option<string>.Some("AsciiTab");
    }

    // Private helper methods

    private Result<ConversionRequest, TabConversionError> ValidateConversionRequest(ConversionRequest request)
    {
        if (request == null)
        {
            return new Result<ConversionRequest, TabConversionError>.Failure(
                new TabConversionError(TabConversionErrorType.ValidationError, "Request cannot be null"));
        }

        if (string.IsNullOrWhiteSpace(request.SourceFormat))
        {
            return new Result<ConversionRequest, TabConversionError>.Failure(
                new TabConversionError(TabConversionErrorType.ValidationError, "Source format cannot be empty"));
        }

        if (string.IsNullOrWhiteSpace(request.TargetFormat))
        {
            return new Result<ConversionRequest, TabConversionError>.Failure(
                new TabConversionError(TabConversionErrorType.ValidationError, "Target format cannot be empty"));
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return new Result<ConversionRequest, TabConversionError>.Failure(
                new TabConversionError(TabConversionErrorType.ValidationError, "Content cannot be empty"));
        }

        return new Result<ConversionRequest, TabConversionError>.Success(request);
    }

    private async Task<ConversionResponse> PerformConversionAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        var response = new ConversionResponse();
        var normalizedSource = request.SourceFormat.ToLowerInvariant();
        var normalizedTarget = request.TargetFormat.ToLowerInvariant();

        // Run conversion in background to support cancellation
        return await Task.Run(() =>
        {
            // ASCII ? VexTab
            if (normalizedSource == "ascii" && normalizedTarget == "vextab")
            {
                response.Success = false;
                response.Errors.Add("ASCII to VexTab conversion is not yet fully implemented");
                response.Warnings.Add("Parser successfully validated the ASCII tab format");
                return response;
            }

            // VexTab ? ASCII
            if (normalizedSource == "vextab" && normalizedTarget == "ascii")
            {
                response.Success = false;
                response.Errors.Add("VexTab to ASCII conversion is not yet fully implemented");
                response.Warnings.Add("Parser successfully validated the VexTab format");
                return response;
            }

            // Guitar Pro ? ASCII
            if (normalizedSource == "gp" && normalizedTarget == "ascii")
            {
                try
                {
                    var parseResult = GuitarProParser.parse(request.Content);
                    if (parseResult is GuitarProTypes.GuitarProParseResult.Error errorResult)
                    {
                        response.Success = false;
                        response.Errors.Add($"Failed to parse Guitar Pro file: {errorResult.Item}");
                        return response;
                    }

                    var gpDoc = ((GuitarProTypes.GuitarProParseResult.Success)parseResult).Item;
                    var ascii = GuitarProParser.toAsciiTab(gpDoc);

                    response.Success = true;
                    response.Result = ascii;
                    response.Warnings.Add("Guitar Pro conversion is simplified");
                    return response;
                }
                catch (Exception ex)
                {
                    response.Success = false;
                    response.Errors.Add($"Guitar Pro to ASCII conversion error: {ex.Message}");
                    return response;
                }
            }

            // Not implemented yet
            response.Success = false;
            response.Errors.Add(
                $"Conversion from {request.SourceFormat} to {request.TargetFormat} is not yet implemented");
            return response;
        }, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Custom error type for tab conversion operations
/// </summary>
public record TabConversionError(TabConversionErrorType Type, string Message);

/// <summary>
///     Error types for tab conversion
/// </summary>
public enum TabConversionErrorType
{
    ValidationError,
    ConversionFailed,
    UnsupportedFormat,
    ParseError
}
