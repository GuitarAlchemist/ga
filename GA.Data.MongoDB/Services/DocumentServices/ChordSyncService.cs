namespace GA.Data.MongoDB.Services.DocumentServices;

using Business.Core.Scales;
using Embeddings;
using Microsoft.Extensions.Logging;
using Models;

[UsedImplicitly]
public class ChordSyncService(
    ILogger<ChordSyncService> logger, 
    MongoDbService mongoDb,
    IEmbeddingService? embeddingService = null) : ISyncService<ChordDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = ChordTemplateFactory.CreateAllChordTemplates()
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
                    Description = GenerateDescription(template),
                    Usage = GenerateUsage(template),
                    Tags = GenerateTags(template),
                    RelatedScales = GetRelatedScales(template),
                    CommonProgressions = GetCommonProgressions(template)
                })
                .ToList();

            // Generate search text and embeddings if service is available
            foreach (var doc in documents)
            {
                doc.GenerateSearchText();
                if (embeddingService != null)
                {
                    doc.Embedding = await embeddingService.GenerateEmbeddingAsync(doc.SearchText);
                }
            }

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

    public async Task<long> GetCountAsync() =>
        await mongoDb.Chords.CountDocumentsAsync(Builders<ChordDocument>.Filter.Empty);

    private static string DetermineQuality(ChordTemplate template)
    {
        // Get the intervals from the pitch class set
        var intervals = template.PitchClassSet.Notes
            .Skip(1)
            .Select(n => template.PitchClassSet.Notes.First().GetInterval(n))
            .ToList();

        // Check for the third and fifth intervals to determine basic quality
        var hasMinorThird = intervals.Any(i => i.ToString() == "m3");
        var hasMajorThird = intervals.Any(i => i.ToString() == "M3");
        var hasDiminishedFifth = intervals.Any(i => i.ToString() == "d5");
        var hasAugmentedFifth = intervals.Any(i => i.ToString() == "A5");
        var hasPerfectFifth = intervals.Any(i => i.ToString() == "P5");

        // Check for seventh intervals
        var hasMinorSeventh = intervals.Any(i => i.ToString() == "m7");
        var hasMajorSeventh = intervals.Any(i => i.ToString() == "M7");

        // Determine the basic quality
        return (hasMinorThird, hasMajorThird, hasDiminishedFifth, hasAugmentedFifth, hasPerfectFifth, hasMinorSeventh, hasMajorSeventh) switch
        {
            // Basic triads
            (true, false, false, false, true, false, false) => "Minor",
            (false, true, false, false, true, false, false) => "Major",
            (true, false, true, false, false, false, false) => "Diminished",
            (false, true, false, true, false, false, false) => "Augmented",

            // Seventh chords
            (true, false, false, false, true, true, false) => "Minor Seventh",
            (false, true, false, false, true, true, false) => "Dominant Seventh",
            (false, true, false, false, true, false, true) => "Major Seventh",
            (true, false, true, false, false, true, false) => "Diminished Seventh",
            (true, false, false, false, true, false, true) => "Minor Major Seventh",

            // Default case for other chord types
            _ => "Other"
        };
    }

    private static string GenerateDescription(ChordTemplate template)
    {
        var quality = DetermineQuality(template);
        var root = template.PitchClassSet.Notes.First().ToString();
        var intervals = template.PitchClassSet.Notes
            .Skip(1)
            .Select(n => template.PitchClassSet.Notes.First().GetInterval(n).ToString())
            .ToList();

        var description = quality switch
        {
            "Major" => $"A {root} major chord consisting of a root, major third, and perfect fifth. " +
                      "This chord has a bright, stable sound and is commonly used as a tonic chord.",
            
            "Minor" => $"A {root} minor chord consisting of a root, minor third, and perfect fifth. " +
                      "This chord has a darker, more melancholic sound compared to its major counterpart.",
            
            "Diminished" => $"A {root} diminished chord consisting of a root, minor third, and diminished fifth. " +
                           "This chord has a tense, unstable sound and often serves as a passing chord.",
            
            "Augmented" => $"A {root} augmented chord consisting of a root, major third, and augmented fifth. " +
                          "This chord has a bright but tense sound and is often used for dramatic effect.",
            
            "Dominant Seventh" => $"A {root} dominant seventh chord consisting of a major triad with an added minor seventh. " +
                                 "This chord creates tension and typically resolves to the tonic.",
            
            "Major Seventh" => $"A {root} major seventh chord consisting of a major triad with an added major seventh. " +
                              "This chord has a rich, complex sound often used in jazz and contemporary music.",
            
            "Minor Seventh" => $"A {root} minor seventh chord consisting of a minor triad with an added minor seventh. " +
                              "This chord has a mellow, jazzy sound commonly used in various musical styles.",
            
            "Diminished Seventh" => $"A {root} diminished seventh chord built entirely of minor thirds. " +
                                   "This highly unstable chord creates maximum tension and often serves as a passing chord.",
            
            "Minor Major Seventh" => $"A {root} minor major seventh chord consisting of a minor triad with an added major seventh. " +
                                    "This chord has a complex, bittersweet sound often used in jazz and contemporary music.",
            
            "Half Diminished Seventh" => $"A {root} half diminished seventh chord consisting of a diminished triad with an added minor seventh. " +
                                        "This chord has a dark, unstable sound often used in minor key progressions.",
            
            _ => $"A {root} chord consisting of the following intervals: {string.Join(", ", intervals)}. " +
                 "This is a more complex harmony with unique sonic characteristics."
        };

        return description;
    }

    private static string GenerateUsage(ChordTemplate template)
    {
        var quality = DetermineQuality(template);
        var root = template.PitchClassSet.Notes.First().ToString();

        return quality switch
        {
            "Major" => $"The {root} major chord is commonly used as:\n" +
                      "- The tonic (I) chord in major keys\n" +
                      "- The dominant (V) chord in the key a perfect fourth below\n" +
                      "- The subdominant (IV) chord in the key a perfect fifth below",

            "Minor" => $"The {root} minor chord is commonly used as:\n" +
                      "- The tonic (i) chord in minor keys\n" +
                      "- The submediant (vi) chord in major keys\n" +
                      "- The mediant (iii) chord in major keys",

            "Diminished" => $"The {root} diminished chord is commonly used as:\n" +
                           "- A passing chord between other harmonies\n" +
                           "- The leading tone chord (vii°) in major and minor keys\n" +
                           "- For creating tension before resolution",

            "Augmented" => $"The {root} augmented chord is commonly used as:\n" +
                          "- A chromatic passing chord\n" +
                          "- An altered dominant chord\n" +
                          "- For creating tension in cadential progressions",

            "Dominant Seventh" => $"The {root} dominant seventh chord is commonly used as:\n" +
                                 "- The dominant (V7) chord resolving to tonic\n" +
                                 "- Secondary dominant chord for temporary modulations\n" +
                                 "- In jazz and blues progressions",

            "Major Seventh" => $"The {root} major seventh chord is commonly used as:\n" +
                              "- The tonic (Imaj7) chord in jazz harmony\n" +
                              "- In jazz ballads and bossa nova\n" +
                              "- For creating lush, stable harmonies",

            "Minor Seventh" => $"The {root} minor seventh chord is commonly used as:\n" +
                              "- The tonic (i7) chord in minor jazz progressions\n" +
                              "- In ii-V-I jazz progressions\n" +
                              "- For creating mellow, jazzy harmonies",

            "Diminished Seventh" => $"The {root} diminished seventh chord is commonly used as:\n" +
                                   "- A passing chord between other harmonies\n" +
                                   "- For modulation between different keys\n" +
                                   "- As a substitute for dominant seventh chords",

            "Minor Major Seventh" => $"The {root} minor major seventh chord is commonly used as:\n" +
                                    "- A tonic chord in minor jazz progressions\n" +
                                    "- For creating complex, ambiguous harmonies\n" +
                                    "- In contemporary and modal jazz",

            "Half Diminished Seventh" => $"The {root} half diminished seventh chord is commonly used as:\n" +
                                        "- The ii chord in minor ii-V-i progressions\n" +
                                        "- As a predominant chord in minor keys\n" +
                                        "- In jazz ballads and modal compositions",

            _ => $"The {root} chord with these intervals can be used:\n" +
                 "- As a color chord for unique harmonic effects\n" +
                 "- In contemporary and experimental music\n" +
                 "- For creating specific harmonic tensions and resolutions"
        };
    }

    private static List<string> GenerateTags(ChordTemplate template)
    {
        var tags = new HashSet<string>();
        var quality = DetermineQuality(template);
        var root = template.PitchClassSet.Notes.First().ToString();
        var noteCount = template.PitchClassSet.Notes.Count;

        // Add basic tags
        tags.Add(quality);
        tags.Add(root);
        tags.Add($"{noteCount}-note");

        // Add category tags
        if (noteCount == 3) tags.Add("triad");
        if (noteCount == 4) tags.Add("seventh");
        if (noteCount > 4) tags.Add("extended");

        // Add specific characteristic tags
        var intervals = template.PitchClassSet.Notes
            .Skip(1)
            .Select(n => template.PitchClassSet.Notes.First().GetInterval(n).ToString())
            .ToList();

        if (intervals.Contains("m3")) tags.Add("minor-third");
        if (intervals.Contains("M3")) tags.Add("major-third");
        if (intervals.Contains("P5")) tags.Add("perfect-fifth");
        if (intervals.Contains("d5")) tags.Add("diminished-fifth");
        if (intervals.Contains("A5")) tags.Add("augmented-fifth");
        if (intervals.Contains("m7")) tags.Add("minor-seventh");
        if (intervals.Contains("M7")) tags.Add("major-seventh");

        // Add functional tags based on quality
        switch (quality)
        {
            case "Major":
            case "Major Seventh":
                tags.Add("tonic");
                tags.Add("bright");
                tags.Add("stable");
                break;

            case "Minor":
            case "Minor Seventh":
                tags.Add("tonic");
                tags.Add("dark");
                tags.Add("melancholic");
                break;

            case "Dominant Seventh":
                tags.Add("dominant");
                tags.Add("tension");
                tags.Add("resolution");
                break;

            case "Diminished":
            case "Diminished Seventh":
            case "Half Diminished Seventh":
                tags.Add("diminished");
                tags.Add("tension");
                tags.Add("unstable");
                tags.Add("passing");
                break;

            case "Augmented":
                tags.Add("augmented");
                tags.Add("tension");
                tags.Add("dramatic");
                break;
        }

        return tags.ToList();
    }

    private static List<RelatedScale> GetRelatedScales(ChordTemplate template)
    {
        var relatedScales = new HashSet<(string Name, List<string> Notes)>();

        // Add directly associated scales from the template
        foreach (var scale in template.AssociatedScales)
        {
            relatedScales.Add((
                scale.ToString(),
                scale.Select(n => n.ToString()).ToList()
            ));
        }

        // Add common scales based on chord quality
        var quality = DetermineQuality(template);
        switch (quality)
        {
            case "Major":
                if (template.AssociatedScales.Any(s => s.ToString().Contains("Major")))
                {
                    var majorScale = template.AssociatedScales.First(s => s.ToString().Contains("Major"));
                    relatedScales.Add((
                        majorScale.ToString(),
                        majorScale.Select(n => n.ToString()).ToList()
                    ));
                }
                break;

            case "Minor":
                foreach (var scale in template.AssociatedScales.Where(s => 
                    s.ToString().Contains("Minor") || 
                    s.ToString().Contains("Harmonic") || 
                    s.ToString().Contains("Melodic")))
                {
                    relatedScales.Add((
                        scale.ToString(),
                        scale.Select(n => n.ToString()).ToList()
                    ));
                }
                break;
        }

        return relatedScales
            .Select(s => new RelatedScale(s.Name, s.Notes))
            .ToList();
    }

    private static List<RelatedProgression> GetCommonProgressions(ChordTemplate template)
    {
        var quality = DetermineQuality(template);
        var root = template.PitchClassSet.Notes.First().ToString();
        var progressions = new List<RelatedProgression>();

        switch (quality)
        {
            case "Major":
                // Common progressions where major chords appear
                progressions.AddRange(new[]
                {
                    new RelatedProgression("I-IV-V", [root, $"F{root}", $"G{root}"]),
                    new RelatedProgression("I-V-vi-IV", [root, $"G{root}", $"A{root}m", $"F{root}"]),
                    new RelatedProgression("I-vi-IV-V", [root, $"A{root}m", $"F{root}", $"G{root}"]),
                });
                break;

            case "Minor":
                progressions.AddRange(new[]
                {
                    new RelatedProgression("ii-V-I", [$"{root}m", $"G{root}", $"C{root}"]),
                    new RelatedProgression("vi-ii-V-I", [$"{root}m", $"D{root}m", $"G{root}", $"C{root}"]),
                    new RelatedProgression("i-iv-v", [$"{root}m", $"D{root}m", $"E{root}m"]),
                });
                break;

            case "Dominant Seventh":
                progressions.AddRange(new[]
                {
                    new RelatedProgression("V7-I", [$"{root}7", $"C{root}"]),
                    new RelatedProgression("ii7-V7-I", [$"D{root}m7", $"{root}7", $"C{root}"]),
                    new RelatedProgression("I7-IV7", [$"{root}7", $"F{root}7"]), // Blues progression
                });
                break;

            case "Diminished":
            case "Diminished Seventh":
                progressions.AddRange(new[]
                {
                    new RelatedProgression("vii°-I", [$"{root}°", $"C{root}"]),
                    new RelatedProgression("ii-vii°-I", [$"D{root}m", $"{root}°", $"C{root}"]),
                });
                break;

            case "Minor Seventh":
                progressions.AddRange(new[]
                {
                    new RelatedProgression("ii7-V7-I", [$"{root}m7", $"G{root}7", $"C{root}"]),
                    new RelatedProgression("i7-iv7-V7", [$"{root}m7", $"D{root}m7", $"G{root}7"]),
                });
                break;

            case "Major Seventh":
                progressions.AddRange(new[]
                {
                    new RelatedProgression("Imaj7-IVmaj7", [$"{root}maj7", $"F{root}maj7"]),
                    new RelatedProgression("iii7-vi7-ii7-V7", [$"{root}maj7", $"A{root}m7", $"D{root}m7", $"G{root}7"]),
                });
                break;

            case "Half Diminished Seventh":
                progressions.AddRange(new[]
                {
                    new RelatedProgression("iiø7-V7-i", [$"{root}ø7", $"G{root}7", $"C{root}m"]),
                    new RelatedProgression("viø7-ii7-V7", [$"{root}ø7", $"D{root}m7", $"G{root}7"]),
                });
                break;
        }

        return progressions;
    }
}
