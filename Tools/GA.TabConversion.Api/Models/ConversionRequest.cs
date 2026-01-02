namespace GA.TabConversion.Api.Models;

/// <summary>
///     Request model for tab format conversion
/// </summary>
public class ConversionRequest
{
    /// <summary>
    ///     Source format (ascii, vextab, midi, musicxml, gp)
    /// </summary>
    public required string SourceFormat { get; set; }

    /// <summary>
    ///     Target format (ascii, vextab, midi, musicxml, gp)
    /// </summary>
    public required string TargetFormat { get; set; }

    /// <summary>
    ///     Content to convert
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    ///     Optional conversion options
    /// </summary>
    public ConversionOptions? Options { get; set; }
}

/// <summary>
///     Conversion options
/// </summary>
public class ConversionOptions
{
    /// <summary>
    ///     Preserve formatting when possible
    /// </summary>
    public bool PreserveFormatting { get; set; } = true;

    /// <summary>
    ///     Include metadata in conversion
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    ///     Strict mode (fail on warnings)
    /// </summary>
    public bool StrictMode { get; set; } = false;

    /// <summary>
    ///     Target tuning (for MIDI conversion)
    /// </summary>
    public string? TargetTuning { get; set; }

    /// <summary>
    ///     Preferred string for pitch (for MIDI conversion)
    /// </summary>
    public int? PreferredString { get; set; }
}

/// <summary>
///     Response model for tab format conversion
/// </summary>
public class ConversionResponse
{
    /// <summary>
    ///     Whether conversion was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Converted content
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    ///     Warnings during conversion
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    ///     Errors during conversion
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    ///     Conversion metadata
    /// </summary>
    public ConversionMetadata? Metadata { get; set; }
}

/// <summary>
///     Conversion metadata
/// </summary>
public class ConversionMetadata
{
    /// <summary>
    ///     Time taken for conversion (ms)
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    ///     Source format detected
    /// </summary>
    public string? DetectedSourceFormat { get; set; }

    /// <summary>
    ///     Number of measures converted
    /// </summary>
    public int? MeasureCount { get; set; }

    /// <summary>
    ///     Number of notes converted
    /// </summary>
    public int? NoteCount { get; set; }
}

/// <summary>
///     Format detection request
/// </summary>
public class DetectFormatRequest
{
    /// <summary>
    ///     Content to analyze
    /// </summary>
    public required string Content { get; set; }
}

/// <summary>
///     Validation request
/// </summary>
public class ValidationRequest
{
    /// <summary>
    ///     Format to validate
    /// </summary>
    public required string Format { get; set; }

    /// <summary>
    ///     Content to validate
    /// </summary>
    public required string Content { get; set; }
}

/// <summary>
///     Validation response
/// </summary>
public class ValidationResponse
{
    /// <summary>
    ///     Whether content is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    ///     Validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    ///     Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    ///     Detected format (if different from requested)
    /// </summary>
    public string? DetectedFormat { get; set; }
}

/// <summary>
///     Format information
/// </summary>
public class FormatInfo
{
    /// <summary>
    ///     Format ID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Display name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     File extensions
    /// </summary>
    public required List<string> Extensions { get; set; }

    /// <summary>
    ///     Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Whether format supports reading
    /// </summary>
    public bool SupportsRead { get; set; }

    /// <summary>
    ///     Whether format supports writing
    /// </summary>
    public bool SupportsWrite { get; set; }

    /// <summary>
    ///     Format category (text, binary, xml)
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
///     Formats list response
/// </summary>
public class FormatsResponse
{
    /// <summary>
    ///     Available formats
    /// </summary>
    public required List<FormatInfo> Formats { get; set; }
}

/// <summary>
///     Format detection response
/// </summary>
public class DetectFormatResponse
{
    /// <summary>
    ///     Detected format name
    /// </summary>
    public required string Format { get; set; }
}

/// <summary>
///     Error response
/// </summary>
public class ErrorResponse
{
    /// <summary>
    ///     Error message
    /// </summary>
    public required string Error { get; set; }
}
