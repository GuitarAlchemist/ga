namespace GA.MusicTheory.Service.Tests;

using System.Net.Http.Json;
using AllProjects.ServiceDefaults;
using GA.MusicTheory.Service.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using Xunit;

public class BasicTheoryTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetKeys_ReturnsMajorAndMinorKeys()
    {
        var response = await _client.GetFromJsonAsync<ApiResponse<IEnumerable<KeyDto>>>("/api/music-theory/keys");
        
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeEmpty();
        response.Data.Any(k => k.Mode == "Major").Should().BeTrue();
        response.Data.Any(k => k.Mode == "Minor").Should().BeTrue();
    }

    [Fact]
    public async Task GetModes_ReturnsSevenDiatonicModes()
    {
        var response = await _client.GetFromJsonAsync<ApiResponse<IEnumerable<ModeDto>>>("/api/music-theory/modes");
        
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().HaveCount(7);
        response.Data.Select(m => m.Name).Should().Contain("Ionian", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Aeolian", "Locrian");
    }

    [Fact]
    public async Task IdentifyChord_CMajor_ReturnsCorrectName()
    {
        // C Major: Root 0, Intervals 0, 4, 7
        var url = "/api/chord-naming/identify?root=0&intervals=0&intervals=4&intervals=7";
        var response = await _client.GetFromJsonAsync<ApiResponse<string>>(url);
        
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().Be("C Major");
    }
}

public record KeyDto
{
    public string Name { get; init; } = "";
    public string Root { get; init; } = "";
    public string Mode { get; init; } = "";
    public int KeySignature { get; init; }
    public string AccidentalKind { get; init; } = "";
    public string[] Notes { get; init; } = [];
}

public record ModeDto
{
    public string Name { get; init; } = "";
    public int Degree { get; init; }
    public bool IsMinor { get; init; }
    public string[] Intervals { get; init; } = [];
    public string[] CharacteristicNotes { get; init; } = [];
}
