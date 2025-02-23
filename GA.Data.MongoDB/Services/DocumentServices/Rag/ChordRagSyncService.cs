namespace GA.Data.MongoDB.Services.DocumentServices.Rag;

using Business.Core.Scales;
using Embeddings;
using Microsoft.Extensions.Logging;
using Models.Rag;

[UsedImplicitly]
public sealed class ChordRagSyncService : ChordSyncService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly MongoDbService _mongoDb;
    private readonly ILogger<ChordRagSyncService> _logger;

    public ChordRagSyncService(
        ILogger<ChordRagSyncService> logger,
        MongoDbService mongoDb,
        IEmbeddingService embeddingService) : base(logger, mongoDb)
    {
        _embeddingService = embeddingService;
        _mongoDb = mongoDb;
        _logger = logger;
    }

    public override async Task<bool> SyncAsync()
    {
        try
        {
            var documents = ChordTemplateFactory.CreateAllChordTemplates()
                .Select(template => new ChordRagDocument
                {
                    Name = template.PitchClassSet.Name,
                    Root = template.PitchClassSet.Notes.First().ToString(),
                    Quality = DetermineQuality(template),
                    Intervals = template.PitchClassSet.Notes
                        .Skip(1)
                        .Select(n => template.PitchClassSet.Notes.First().GetInterval(n).ToString())
                        .ToList(),
                    Notes = template.PitchClassSet.Notes.Select(n => n.ToString()).ToList(),
                    Description = GenerateDescription(template),
                    Usage = GenerateUsage(template),
                    Tags = GenerateTags(template),
                    RelatedScales = GetRelatedScales(template),
                    CommonProgressions = GetCommonProgressions(template)
                })
                .ToList();

            // Generate search text and embeddings
            foreach (var doc in documents)
            {
                doc.GenerateSearchText();
                doc.Embedding = await _embeddingService.GenerateEmbeddingAsync(doc.SearchText);
            }

            await _mongoDb.ChordsRag.DeleteManyAsync(Builders<ChordRagDocument>.Filter.Empty);
            await _mongoDb.ChordsRag.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing RAG chords");
            return false;
        }
    }

    public override async Task<long> GetCountAsync() =>
        await _mongoDb.ChordsRag.CountDocumentsAsync(Builders<ChordRagDocument>.Filter.Empty);

    private static string GenerateDescription(ChordTemplate template)
    {
        var quality = DetermineQuality(template);
        var root = template.PitchClassSet.Notes.First().ToString();

        return quality switch
        {
            "Major" => $"The {root} major chord is a bright, stable chord built on the root note {root}. " +
                       "It consists of a major third and perfect fifth above the root.",

            "Minor" => $"The {root} minor chord has a darker, more melancholic sound than its major counterpart. " +
                       "It's built with a minor third and perfect fifth above the root note {root}.",

            "Diminished" => $"The {root} diminished chord creates tension and instability. " +
                            "It contains a minor third and diminished fifth above {root}.",

            "Augmented" => $"The {root} augmented chord has a mysterious, unsettled quality. " +
                           "It's formed with a major third and augmented fifth above {root}.",

            "Dominant Seventh" => $"The {root}7 dominant seventh chord creates strong tension seeking resolution. " +
                                  "It adds a minor seventh to a major triad built on {root}.",

            "Major Seventh" => $"The {root}maj7 major seventh chord has a rich, complex sound. " +
                               "It combines a major triad with a major seventh above {root}.",

            "Minor Seventh" => $"The {root}m7 minor seventh chord is commonly used in jazz and popular music. " +
                               "It adds a minor seventh to a minor triad based on {root}.",

            _ => $"The {root} {quality.ToLower()} chord is a specialized harmony built on {root}."
        };
    }

    private static string GenerateUsage(ChordTemplate template)
    {
        var quality = DetermineQuality(template);
        var root = template.PitchClassSet.Notes.First().ToString();

        return quality switch
        {
            "Major" => $"The {root} major chord is fundamental in many genres. It's commonly used as a tonic chord " +
                       "and appears in countless progressions. Especially effective in pop, rock, and classical music.",

            "Minor" => $"The {root} minor chord is essential in creating emotional depth. Popular in ballads, " +
                       "rock music, and classical pieces. Often used to express sadness or introspection.",

            "Diminished" => $"The {root} diminished chord typically serves as a passing chord or creates tension. " +
                            "Common in jazz and classical music, especially as a leading tone chord.",

            "Augmented" => $"The {root} augmented chord adds color and tension. Often used in jazz and " +
                           "experimental music, or as a passing chord in classical pieces.",

            "Dominant Seventh" => $"The {root}7 chord is crucial in jazz and blues. It creates tension that " +
                                  "typically resolves to the tonic, and is essential in II-V-I progressions.",

            "Major Seventh" => $"The {root}maj7 chord adds sophistication to harmonies. Popular in jazz, " +
                               "bossa nova, and modern pop music for its rich, complex sound.",

            "Minor Seventh" => $"The {root}m7 chord is versatile and widely used in jazz, soul, and R&B. " +
                               "It works well in both rhythm and melody contexts.",

            _ => $"The {root} {quality.ToLower()} chord can be used for specialized harmonic effects."
        };
    }
    
    private static List<string> GenerateTags(ChordTemplate template)
    {
        var tags = new HashSet<string>();
        var quality = DetermineQuality(template);
        var root = template.PitchClassSet.Notes.First().ToString();
    
        // Add basic quality tags
        tags.Add(quality);
    
        // Add category-based tags
        switch (quality)
        {
            case "Major":
            case "Minor":
                tags.Add("Basic Triad");
                tags.Add("Common Chord");
                tags.Add("Diatonic");
                break;
            
            case "Diminished":
            case "Augmented":
                tags.Add("Tension Chord");
                tags.Add("Altered Chord");
                break;
            
            case "Dominant Seventh":
            case "Major Seventh":
            case "Minor Seventh":
                tags.Add("Seventh Chord");
                tags.Add("Extended Harmony");
                tags.Add("Jazz");
                break;
        }

        // Add root-based tags
        if (root.Contains("b"))
            tags.Add("Flat Key");
        else if (root.Contains("#"))
            tags.Add("Sharp Key");
        else
            tags.Add("Natural Key");

        // Add interval-based tags
        var intervals = template.PitchClassSet.Notes
            .Skip(1)
            .Select(n => template.PitchClassSet.Notes.First().GetInterval(n));

        var enumerable = intervals.ToList();
        if (enumerable.Any(i => i.ToString() == "m3"))
            tags.Add("Minor Third");
        if (enumerable.Any(i => i.ToString() == "M3"))
            tags.Add("Major Third");
        if (enumerable.Any(i => i.ToString() == "P5"))
            tags.Add("Perfect Fifth");
        if (enumerable.Any(i => i.ToString() == "d5"))
            tags.Add("Diminished Fifth");
        if (enumerable.Any(i => i.ToString() == "A5"))
            tags.Add("Augmented Fifth");
        if (enumerable.Any(i => i.ToString().Contains("7")))
            tags.Add("Seventh");

        return tags.OrderBy(t => t).ToList();
    }
}