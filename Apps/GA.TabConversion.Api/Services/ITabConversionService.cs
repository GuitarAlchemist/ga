namespace GA.TabConversion.Api.Services;

using Models;

/// <summary>
///     Service for converting between guitar tab formats
/// </summary>
public interface ITabConversionService
{
    /// <summary>
    ///     Convert tab from one format to another
    /// </summary>
    Task<ConversionResponse> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validate tab content
    /// </summary>
    Task<ValidationResponse> ValidateAsync(ValidationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get list of supported formats
    /// </summary>
    Task<FormatsResponse> GetFormatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Detect format from content
    /// </summary>
    Task<string?> DetectFormatAsync(string content, CancellationToken cancellationToken = default);
}
