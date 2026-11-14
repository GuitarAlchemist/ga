namespace GA.Business.Core.Tests.Configuration;

using GA.Domain.Services;

[TestFixture]
public class MusicalKnowledgeStatisticsTests
{
    private static int Entries(string artist) =>
        IconicChordsService.FindChordsByArtist(artist).Count() +
        ChordProgressionsService.FindProgressionsByArtist(artist).Count() +
        GuitarTechniquesService.FindTechniquesByArtist(artist).Count() +
        SpecializedTuningsService.FindTuningsByArtist(artist).Count();

    [Test]
    public void ReloadAllConfigurations_DoesNotThrow()
    {
        // IconicChordsConfigLoader set its static readonly field through reflection, which throws FieldAccessException
        // since .NET Core 3.0, so ReloadAllConfigurations stopped before the three other loaders
        Assert.DoesNotThrow(MusicalKnowledgeService.ReloadAllConfigurations);
        Assert.That(IconicChordsService.GetAllChords(), Is.Not.Empty);
    }

    [Test]
    public void ArtistBreakdown_KeepsTheArtistsWithTheMostEntries_MostFirst()
    {
        // The breakdown took the first 20 artists in alphabetical order, then sorted them by count
        var breakdown = MusicalKnowledgeService.GetStatistics().ArtistBreakdown.ToList();
        var counts = breakdown.Select(entry => entry.Value).ToList();
        var leftOut = MusicalKnowledgeService.GetAllArtists().Except(breakdown.Select(entry => entry.Key)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(breakdown, Has.Count.InRange(1, 20));
            Assert.That(counts, Is.Ordered.Descending);
            Assert.That(leftOut.Select(Entries), Has.All.LessThanOrEqualTo(counts[^1]));
        });
    }
}
