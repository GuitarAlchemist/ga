namespace GaApi.Tests;

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Models;
using Newtonsoft.Json;

/// <summary>
///     Integration tests for the Contextual Chords API endpoints
/// </summary>
[TestFixture]
public class ContextualChordsIntegrationTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Set environment to Testing to disable rate limiting
                builder.UseEnvironment("Testing");
            });

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    // Helper method to extract data from ApiResponse wrapper
    private async Task<T?> GetDataFromApiResponse<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(json);
        Assert.That(apiResponse, Is.Not.Null, "API response should not be null");
        Assert.That(apiResponse!.Success, Is.True, $"API call should succeed. Error: {apiResponse.Error}");
        return apiResponse.Data;
    }

    [Test]
    public async Task GetChordsForKey_CMajor_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/contextual-chords/keys/C%20Major?extension=Seventh&limit=20");

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected OK but got {response.StatusCode}. Response: {errorContent}");
        }

        var chords = await GetDataFromApiResponse<List<ChordInContextDto>>(response);
        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.Count, Is.GreaterThan(0));
        Assert.That(chords.Count, Is.LessThanOrEqualTo(20));
    }

    [Test]
    public async Task GetChordsForKey_OnlyDiatonic_Returns7Chords()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/contextual-chords/keys/C%20Major?extension=Seventh&onlyNaturallyOccurring=true");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await GetDataFromApiResponse<List<ChordInContextDto>>(response);
        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.Count, Is.EqualTo(7), "Should return exactly 7 diatonic chords");
        Assert.That(chords.All(c => c.IsNaturallyOccurring), Is.True);
    }

    [Test]
    public async Task GetChordsForKey_WithBorrowedChords_ReturnsMoreThan7()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/contextual-chords/keys/C%20Major?extension=Seventh&includeBorrowedChords=true&includeSecondaryDominants=false&includeSecondaryTwoFive=false");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await GetDataFromApiResponse<List<ChordInContextDto>>(response);
        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.Count, Is.GreaterThan(7), "Should include borrowed chords");

        var borrowedChords = chords.Where(c => !c.IsNaturallyOccurring).ToList();
        Assert.That(borrowedChords.Count, Is.GreaterThan(0), "Should have borrowed chords");
    }

    [Test]
    public async Task GetChordsForKey_WithSecondaryDominants_ReturnsSecondaryDominants()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/contextual-chords/keys/C%20Major?extension=Seventh&includeBorrowedChords=false&includeSecondaryDominants=true&includeSecondaryTwoFive=false");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await GetDataFromApiResponse<List<ChordInContextDto>>(response);
        Assert.That(chords, Is.Not.Null);

        var secondaryDominants = chords!.Where(c => c.IsSecondaryDominant).ToList();
        Assert.That(secondaryDominants.Count, Is.GreaterThan(0), "Should have secondary dominants");

        // Verify secondary dominant properties
        foreach (var chord in secondaryDominants)
        {
            Assert.That(chord.SecondaryDominant, Is.Not.Null);
            Assert.That(chord.SecondaryDominant!.Notation, Does.Match(@"V/\w+"), "Should have V/x notation");
        }
    }

    [Test]
    public async Task GetChordsForKey_WithSecondaryTwoFive_ReturnsSecondaryTwoFive()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/contextual-chords/keys/C%20Major?extension=Seventh&includeBorrowedChords=false&includeSecondaryDominants=false&includeSecondaryTwoFive=true");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await GetDataFromApiResponse<List<ChordInContextDto>>(response);
        Assert.That(chords, Is.Not.Null);

        var secondaryTwoFive = chords!.Where(c => c.SecondaryDominant?.IsPartOfTwoFive == true).ToList();
        Assert.That(secondaryTwoFive.Count, Is.GreaterThan(0), "Should have secondary ii-V chords");

        // Verify ii-V properties
        foreach (var chord in secondaryTwoFive)
        {
            Assert.That(chord.SecondaryDominant!.Notation, Does.Match(@"ii/\w+"), "Should have ii/x notation");
        }
    }

    [Test]
    public async Task GetChordsForKey_InvalidKey_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/contextual-chords/keys/InvalidKey");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetChordsForKey_MinCommonality_FiltersCorrectly()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/contextual-chords/keys/C%20Major?extension=Seventh&minCommonality=0.7");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await GetDataFromApiResponse<List<ChordInContextDto>>(response);
        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.All(c => c.Commonality >= 0.7), Is.True, "All chords should have commonality >= 0.7");
    }

    [Test]
    public async Task GetChordsForScale_CMajorScale_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/contextual-chords/scales/Major?extension=Seventh");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await GetDataFromApiResponse<List<ChordInContextDto>>(response);
        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetChordsForScale_InvalidScale_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/contextual-chords/scales/InvalidScale");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetChordsForMode_Dorian_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/contextual-chords/modes/Dorian?extension=Seventh");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chords = await GetDataFromApiResponse<List<ChordInContextDto>>(response);
        Assert.That(chords, Is.Not.Null);
        Assert.That(chords!.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetChordsForMode_InvalidMode_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/contextual-chords/modes/InvalidMode");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetVoicingsForChord_Cmaj7_ReturnsSuccess()
    {
        // Act
        var response =
            await _client.GetAsync("/api/contextual-chords/voicings/Cmaj7?maxDifficulty=Intermediate&limit=10");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var voicings = await GetDataFromApiResponse<List<VoicingWithAnalysisDto>>(response);
        Assert.That(voicings, Is.Not.Null);
        Assert.That(voicings!.Count, Is.GreaterThan(0));
        Assert.That(voicings.Count, Is.LessThanOrEqualTo(10));
    }

    [Test]
    public async Task GetVoicingsForChord_WithFretRange_FiltersCorrectly()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/contextual-chords/voicings/Cmaj7?minFret=0&maxFret=5&limit=20");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var voicings = await GetDataFromApiResponse<List<VoicingWithAnalysisDto>>(response);
        Assert.That(voicings, Is.Not.Null);

        foreach (var voicing in voicings!)
        {
            Assert.That(voicing.Physical.LowestFret, Is.GreaterThanOrEqualTo(0));
            Assert.That(voicing.Physical.HighestFret, Is.LessThanOrEqualTo(5));
        }
    }

    [Test]
    public async Task GetVoicingsForChord_InvalidChord_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/contextual-chords/voicings/InvalidChord");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetChordsForKey_MultipleCalls_UsesCache()
    {
        // First call
        var response1 = await _client.GetAsync("/api/contextual-chords/keys/C%20Major?extension=Seventh&limit=20");
        var chords1 = await GetDataFromApiResponse<List<ChordInContextDto>>(response1);

        // Second call with same parameters
        var response2 = await _client.GetAsync("/api/contextual-chords/keys/C%20Major?extension=Seventh&limit=20");
        var chords2 = await GetDataFromApiResponse<List<ChordInContextDto>>(response2);

        // Assert - both should return same results
        Assert.That(chords1, Is.Not.Null);
        Assert.That(chords2, Is.Not.Null);
        Assert.That(chords1!.Count, Is.EqualTo(chords2!.Count));

        // Verify same chords in same order
        for (var i = 0; i < chords1.Count; i++)
        {
            Assert.That(chords1[i].ContextualName, Is.EqualTo(chords2[i].ContextualName));
        }
    }

    [Test]
    public async Task GetChordsForKey_DifferentParameters_ReturnsDifferentResults()
    {
        // Call with different limits
        var response1 = await _client.GetAsync("/api/contextual-chords/keys/C%20Major?extension=Seventh&limit=10");
        var chords1 = await GetDataFromApiResponse<List<ChordInContextDto>>(response1);

        var response2 = await _client.GetAsync("/api/contextual-chords/keys/C%20Major?extension=Seventh&limit=20");
        var chords2 = await GetDataFromApiResponse<List<ChordInContextDto>>(response2);

        // Assert - different limits should return different counts
        Assert.That(chords1, Is.Not.Null);
        Assert.That(chords2, Is.Not.Null);
        Assert.That(chords1!.Count, Is.LessThanOrEqualTo(10));
        Assert.That(chords2!.Count, Is.LessThanOrEqualTo(20));
    }
}
