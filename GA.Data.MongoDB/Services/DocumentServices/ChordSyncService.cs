namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core.Chords;
using Microsoft.Extensions.Logging;
using Models;
using Models.References;

[UsedImplicitly]
public class ChordSyncService(
    ILogger<ChordSyncService> logger,
    MongoDbService mongoDb) : ISyncService<ChordDocument>
{
    public virtual async Task<bool> SyncAsync()
    {
        try
        {
            var documents = ChordTemplateFactory.GenerateAllPossibleChords()
                .Select(template => new ChordDocument
                {
                    Name = template.PitchClassSet.Name,
                    Root = template.PitchClassSet.Notes.First().ToString(),
                    Quality = DetermineQuality(template),
                    Intervals = template.PitchClassSet.Notes
                        .Skip(1)
                        .Select(n => template.PitchClassSet.Notes.First().GetInterval(n).ToString())
                        .ToList(),
                    Notes = template.PitchClassSet.Notes.Select(n => n.ToString()).ToList(),
                    RelatedScales = GetRelatedScales(template),
                    CommonProgressions = GetCommonProgressions(template)
                })
                .ToList();

            await mongoDb.Chords.DeleteManyAsync(Builders<ChordDocument>.Filter.Empty);
            await mongoDb.Chords.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing chords");
            return false;
        }
    }

    public virtual async Task<long> GetCountAsync()
    {
        return await mongoDb.Chords.CountDocumentsAsync(Builders<ChordDocument>.Filter.Empty);
    }

    protected static string DetermineQuality(ChordTemplate template)
    {
        var intervals = template.PitchClassSet.Notes
            .Skip(1)
            .Select(n => template.PitchClassSet.Notes.First().GetInterval(n))
            .ToList();

        var hasMinorThird = intervals.Any(i => i.ToString() == "m3");
        var hasMajorThird = intervals.Any(i => i.ToString() == "M3");
        var hasDiminishedFifth = intervals.Any(i => i.ToString() == "d5");
        var hasAugmentedFifth = intervals.Any(i => i.ToString() == "A5");
        var hasPerfectFifth = intervals.Any(i => i.ToString() == "P5");
        var hasMinorSeventh = intervals.Any(i => i.ToString() == "m7");
        var hasMajorSeventh = intervals.Any(i => i.ToString() == "M7");

        return (hasMinorThird, hasMajorThird, hasDiminishedFifth, hasAugmentedFifth, hasPerfectFifth, hasMinorSeventh,
                hasMajorSeventh) switch
            {
                (true, false, false, false, true, false, false) => "Minor",
                (false, true, false, false, true, false, false) => "Major",
                (true, false, true, false, false, false, false) => "Diminished",
                (false, true, false, true, false, false, false) => "Augmented",
                (true, false, false, false, true, true, false) => "Minor Seventh",
                (false, true, false, false, true, true, false) => "Dominant Seventh",
                (false, true, false, false, true, false, true) => "Major Seventh",
                (true, false, true, false, false, true, false) => "Diminished Seventh",
                (true, false, false, false, true, false, true) => "Minor Major Seventh",
                _ => "Other"
            };
    }

    protected static List<ScaleReference> GetRelatedScales(ChordTemplate template)
    {
        // The previous implementation relied on an 'AssociatedScales' property that no longer exists.
        // To keep synchronization working and the project compiling, we return an empty list for now.
        // This can be enhanced later by inferring related scales from the chord's pitch class set.
        return new List<ScaleReference>();
    }

    protected static List<ProgressionReference> GetCommonProgressions(ChordTemplate template)
    {
        var quality = DetermineQuality(template);
        var root = template.PitchClassSet.Notes.First().ToString();

        return quality switch
        {
            "Major" =>
            [
                new("I-IV-V", [root, $"F{root}", $"G{root}"]),
                new("I-V-vi-IV", [root, $"G{root}", $"A{root}m", $"F{root}"]),
                new("I-vi-IV-V", [root, $"A{root}m", $"F{root}", $"G{root}"])
            ],
            "Minor" =>
            [
                new("ii-V-I", [$"{root}m", $"G{root}", $"C{root}"]),
                new("i-iv-v", [$"{root}m", $"D{root}m", $"E{root}m"])
            ],
            "Dominant Seventh" => [new("V7-I", [$"{root}7", $"C{root}"])],
            _ => []
        };
    }
}
