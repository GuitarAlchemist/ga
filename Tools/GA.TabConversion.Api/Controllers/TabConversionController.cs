namespace GA.TabConversion.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Models;
using Services;

/// <summary>
///     API controller for guitar tab format conversion
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TabConversionController(ITabConversionService conversionService,
    ILogger<TabConversionController> logger) : ControllerBase
{
    /// <summary>
    ///     Convert tab from one format to another
    /// </summary>
    /// <param name="request">Conversion request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conversion response</returns>
    [HttpPost("convert")]
    [ProducesResponseType(typeof(ConversionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ConversionResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ConversionResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConversionResponse>> Convert(
        [FromBody] ConversionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new ConversionResponse
                {
                    Success = false,
                    Errors = ["Request cannot be null"]
                });
            }

            // Validate request content
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new ConversionResponse
                {
                    Success = false,
                    Errors = ["Content cannot be empty"]
                });
            }

            logger.LogInformation("Conversion request: {Source} -> {Target}",
                request.SourceFormat, request.TargetFormat);

            var response = await conversionService.ConvertAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid argument in conversion request");
            return BadRequest(new ConversionResponse
            {
                Success = false,
                Errors = [ex.Message]
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing conversion request");
            return StatusCode(500, new ConversionResponse
            {
                Success = false,
                Errors = ["Internal server error during conversion"]
            });
        }
    }

    /// <summary>
    ///     Validate tab content
    /// </summary>
    /// <param name="request">Validation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation response</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ValidationResponse>> Validate(
        [FromBody] ValidationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new ValidationResponse
                {
                    IsValid = false,
                    Errors = ["Request cannot be null"]
                });
            }

            logger.LogInformation("Validation request for format: {Format}", request.Format);

            var response = await conversionService.ValidateAsync(request, cancellationToken).ConfigureAwait(false);

            // Return BadRequest if format is unsupported
            if (response.Errors.Any(e => e.Contains("Unsupported format", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing validation request");
            return StatusCode(500, new ValidationResponse
            {
                IsValid = false,
                Errors = ["Internal server error during validation"]
            });
        }
    }

    /// <summary>
    ///     Get list of supported formats
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of supported format names</returns>
    [HttpGet("formats")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<string>>> GetFormats(CancellationToken cancellationToken)
    {
        try
        {
            var response = await conversionService.GetFormatsAsync(cancellationToken).ConfigureAwait(false);
            // Return the display names (e.g., "VexTab", "ASCII Tab") instead of IDs
            var formatNames = response.Formats.Select(f => f.Name).ToList();
            return Ok(formatNames);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting formats");
            return StatusCode(500, new List<string>());
        }
    }

    /// <summary>
    ///     Detect format from content
    /// </summary>
    /// <param name="request">Request containing content to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected format</returns>
    [HttpPost("detect-format")]
    [ProducesResponseType(typeof(DetectFormatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DetectFormatResponse>> DetectFormat(
        [FromBody] DetectFormatRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new ErrorResponse { Error = "Request cannot be null" });
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new ErrorResponse { Error = "Content cannot be empty" });
            }

            var format = await conversionService.DetectFormatAsync(request.Content, cancellationToken)
                .ConfigureAwait(false);
            return Ok(new DetectFormatResponse { Format = format ?? "Unknown" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error detecting format");
            return StatusCode(500, new ErrorResponse { Error = "Internal server error during format detection" });
        }
    }

    /// <summary>
    ///     Health check endpoint
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
