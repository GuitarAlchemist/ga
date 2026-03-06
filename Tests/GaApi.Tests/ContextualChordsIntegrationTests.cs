namespace GaApi.Tests;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using NUnit.Framework;
using GaApi.Models;
using System.Collections.Generic;

[TestFixture]
public class ContextualChordsIntegrationTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
            });

        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetChordsForKey_ReturnsSuccessAndChords()
    {
        var response = await _client.GetAsync("/api/contextual-chords/keys/C Major");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        var chords = JsonSerializer.Deserialize<List<ChordInContext>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.Count, Is.EqualTo(7));
        Assert.That(chords[0].ContextualName, Is.EqualTo("C"));
    }

    [Test]
    public async Task GetChordsForScale_ReturnsSuccessAndChords()
    {
        var response = await _client.GetAsync("/api/contextual-chords/scales/Major/G");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        var chords = JsonSerializer.Deserialize<List<ChordInContext>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.Count, Is.EqualTo(7));
        Assert.That(chords[0].ContextualName, Is.EqualTo("G"));
        Assert.That(chords[1].ContextualName, Is.EqualTo("Am"));
    }

    [Test]
    public async Task GetChordsForMode_ReturnsSuccessAndChords()
    {
        var response = await _client.GetAsync("/api/contextual-chords/modes/Dorian/D");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        var chords = JsonSerializer.Deserialize<List<ChordInContext>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.Count, Is.EqualTo(7));
        Assert.That(chords[0].ContextualName, Is.EqualTo("Dm"));
    }

    [Test]
    public async Task GetVoicingsForChord_ReturnsSuccessAndVoicings()
    {
        var response = await _client.GetAsync("/api/contextual-chords/voicings/C%20Major");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        var voicings = JsonSerializer.Deserialize<List<VoicingWithAnalysis>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(voicings, Is.Not.Null);
        Assert.That(voicings!.Count, Is.GreaterThan(0));
    }
}
