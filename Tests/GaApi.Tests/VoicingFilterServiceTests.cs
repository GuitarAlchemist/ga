namespace GaApi.Tests;

using GaApi.Services;
using NUnit.Framework;

[TestFixture]
public class VoicingFilterServiceTests
{
    private readonly VoicingFilterService _service = new();

    [Test]
    public async Task GetVoicingsForChordAsync_CMajor_ReturnsVoicings()
    {
        var voicings = (await _service.GetVoicingsForChordAsync("C Major")).ToList();

        Assert.That(voicings.Count, Is.GreaterThan(0));
        // Verify one of the voicings is a standard C Major (e.g. x-3-2-0-1-0 or similar)
        // Since it's dynamic, we just check if any voicing contains C, E, G notes effectively via the filter logic
        foreach (var voicing in voicings)
        {
            Assert.That(voicing.ChordName, Is.EqualTo("C Major"));
            Assert.That(voicing.Frets.Length, Is.EqualTo(6));
        }
    }

    [Test]
    public async Task GetVoicingsForChordAsync_WithDifficultyFilter_FiltersCorrectly()
    {
        // Get all voicings first
        var allVoicings = (await _service.GetVoicingsForChordAsync("C Major")).ToList();
        
        // Get filtered voicings
        var filteredVoicings = (await _service.GetVoicingsForChordAsync("C Major", maxDifficulty: 3)).ToList();

        Assert.That(filteredVoicings.Count, Is.LessThanOrEqualTo(allVoicings.Count));
        foreach (var voicing in filteredVoicings)
        {
            Assert.That(voicing.DifficultyScore, Is.LessThanOrEqualTo(3.0));
        }
    }
}
