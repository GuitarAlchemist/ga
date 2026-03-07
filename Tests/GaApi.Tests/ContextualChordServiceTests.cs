namespace GaApi.Tests;

using GaApi.Services;
using NUnit.Framework;

[TestFixture]
public class ContextualChordServiceTests
{
    private readonly ContextualChordService _service = new();

    [Test]
    public async Task GetChordsForKeyAsync_CMajor_ReturnsDiatonicChords()
    {
        var chords = (await _service.GetChordsForKeyAsync("C Major")).ToList();

        Assert.That(chords.Count, Is.EqualTo(7));
        Assert.That(chords[0].ContextualName, Is.EqualTo("C"));
        Assert.That(chords[1].ContextualName, Is.EqualTo("Dm"));
        Assert.That(chords[2].ContextualName, Is.EqualTo("Em"));
        Assert.That(chords[3].ContextualName, Is.EqualTo("F"));
        Assert.That(chords[4].ContextualName, Is.EqualTo("G"));
        Assert.That(chords[5].ContextualName, Is.EqualTo("Am"));
        Assert.That(chords[6].ContextualName, Is.EqualTo("Bdim"));
    }

    [Test]
    public async Task GetChordsForKeyAsync_AMinor_ReturnsDiatonicChords()
    {
        var chords = (await _service.GetChordsForKeyAsync("Am")).ToList();

        Assert.That(chords.Count, Is.EqualTo(7));
        Assert.That(chords[0].ContextualName, Is.EqualTo("Am"));
        Assert.That(chords[1].ContextualName, Is.EqualTo("Bdim"));
        Assert.That(chords[2].ContextualName, Is.EqualTo("C"));
        Assert.That(chords[3].ContextualName, Is.EqualTo("Dm"));
        Assert.That(chords[4].ContextualName, Is.EqualTo("Em"));
        Assert.That(chords[5].ContextualName, Is.EqualTo("F"));
        Assert.That(chords[6].ContextualName, Is.EqualTo("G"));
    }
}
