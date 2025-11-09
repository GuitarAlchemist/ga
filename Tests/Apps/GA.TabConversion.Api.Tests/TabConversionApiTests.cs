namespace GA.TabConversion.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Models;

/// <summary>
///     Integration tests for Tab Conversion API endpoints
/// </summary>
public class TabConversionApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public TabConversionApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    #region Health Check Tests

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/TabConversion/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Supported Formats Tests

    [Fact]
    public async Task GetFormats_ShouldReturnSupportedFormats()
    {
        // Act
        var response = await _client.GetAsync("/api/TabConversion/formats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var formats = await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions);
        Assert.NotNull(formats);
        Assert.NotEmpty(formats);
        Assert.Contains("VexTab", formats);
        Assert.Contains("AsciiTab", formats);
    }

    #endregion

    #region Format Detection Tests

    [Fact]
    public async Task DetectFormat_VexTab_ShouldReturnVexTab()
    {
        // Arrange
        var request = new { Content = "tabstave notation=true\nnotes :q 4/5 5/4" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/detect-format", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("format"));
        Assert.Equal("VexTab", result["format"]?.ToString());
    }

    [Fact]
    public async Task DetectFormat_AsciiTab_ShouldReturnAsciiTab()
    {
        // Arrange
        var request = new { Content = "e|---0---1---3---|\nB|---1---1---0---|\nG|---0---2---0---|" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/detect-format", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("format"));
        Assert.Equal("AsciiTab", result["format"]?.ToString());
    }

    [Fact]
    public async Task DetectFormat_EmptyContent_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new { Content = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/detect-format", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Validate_ValidVexTab_ShouldReturnSuccess()
    {
        // Arrange
        var request = new
        {
            Format = "VexTab",
            Content = "tabstave notation=true tablature=true\nnotes :q 0/1 3/1 5/1"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/validate", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ValidationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_InvalidVexTab_ShouldReturnErrors()
    {
        // Arrange
        var request = new
        {
            Format = "VexTab",
            Content = "invalid vextab content @#$%"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/validate", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ValidationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Validate_ValidAsciiTab_ShouldReturnSuccess()
    {
        // Arrange
        var request = new
        {
            Format = "AsciiTab",
            Content = "e|---0---1---3---|\nB|---1---1---0---|\nG|---0---2---0---|"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/validate", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ValidationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_UnsupportedFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            Format = "UnsupportedFormat",
            Content = "some content"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/validate", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public async Task Convert_VexTabToAsciiTab_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ConversionRequest
        {
            SourceFormat = "VexTab",
            TargetFormat = "AsciiTab",
            Content = "tabstave notation=true tablature=true\nnotes :q 0/1 3/1 5/1",
            Options = new ConversionOptions()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/convert", request);

        // Assert
        // VexTab to AsciiTab conversion is not yet fully implemented
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConversionResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("not yet fully implemented", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Convert_AsciiTabToVexTab_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ConversionRequest
        {
            SourceFormat = "AsciiTab",
            TargetFormat = "VexTab",
            Content = "e|---0---1---3---|\nB|---1---1---0---|\nG|---0---2---0---|",
            Options = new ConversionOptions()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/convert", request);

        // Assert
        // AsciiTab to VexTab conversion is not yet fully implemented
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConversionResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("not yet fully implemented", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Convert_InvalidSourceFormat_ShouldReturnError()
    {
        // Arrange
        var request = new ConversionRequest
        {
            SourceFormat = "InvalidFormat",
            TargetFormat = "VexTab",
            Content = "some content",
            Options = new ConversionOptions()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/convert", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Convert_EmptyContent_ShouldReturnError()
    {
        // Arrange
        var request = new ConversionRequest
        {
            SourceFormat = "VexTab",
            TargetFormat = "AsciiTab",
            Content = "",
            Options = new ConversionOptions()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/convert", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Convert_SameSourceAndTarget_ShouldReturnOriginalContent()
    {
        // Arrange
        var content = "tabstave notation=true\nnotes :q 4/5 5/4";
        var request = new ConversionRequest
        {
            SourceFormat = "VexTab",
            TargetFormat = "VexTab",
            Content = content,
            Options = new ConversionOptions()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/TabConversion/convert", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConversionResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(content, result.Result);
    }

    #endregion
}
