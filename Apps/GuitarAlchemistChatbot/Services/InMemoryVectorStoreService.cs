namespace GuitarAlchemistChatbot.Services;

using System.Collections.Concurrent;
using GA.Business.Core.Chords;
using GA.Business.Core.Intervals;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     In-memory vector store for semantic search over music theory knowledge
/// </summary>
public class InMemoryVectorStoreService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<InMemoryVectorStoreService> logger,
    ILoggerFactory? loggerFactory = null)
{
    private readonly NonEuclideanSimilarityService _similarityService =
        new((loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<NonEuclideanSimilarityService>());

    private readonly ConcurrentDictionary<string, VectorEntry> _vectors = new();

    public bool IsIndexed { get; private set; }

    public DateTime? LastIndexedTime { get; private set; }

    public int DocumentCount { get; private set; }

    /// <summary>
    ///     Index music theory knowledge base
    /// </summary>
    public async Task<IndexingResult> IndexKnowledgeBaseAsync()
    {
        logger.LogInformation("Starting vector store indexing...");
        var startTime = DateTime.UtcNow;

        try
        {
            _vectors.Clear();

            // Music theory knowledge base
            var documents = GetMusicTheoryDocuments();

            logger.LogInformation("Generating embeddings for {Count} documents...", documents.Count);

            var tasks = documents.Select(async doc =>
            {
                try
                {
                    var embedding = await GenerateEmbeddingAsync(doc.Content);
                    _vectors[doc.Id] = new VectorEntry(doc.Id, doc.Content, doc.Category, embedding);
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate embedding for document {Id}", doc.Id);
                    return false;
                }
            });

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            IsIndexed = true;
            LastIndexedTime = DateTime.UtcNow;
            DocumentCount = successCount;

            var duration = DateTime.UtcNow - startTime;

            logger.LogInformation(
                "Vector store indexed successfully: {Count} documents in {Duration}ms",
                successCount, duration.TotalMilliseconds);

            return new IndexingResult(
                true,
                successCount,
                duration,
                $"Successfully indexed {successCount} documents");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index vector store");
            return new IndexingResult(
                false,
                0,
                DateTime.UtcNow - startTime,
                $"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Search for relevant documents using semantic similarity
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, int topK = 5)
    {
        if (!IsIndexed)
        {
            logger.LogWarning("Vector store not indexed. Call IndexKnowledgeBaseAsync first.");
            return [];
        }

        try
        {
            var queryEmbedding = await GenerateEmbeddingAsync(query);

            var results = _vectors.Values
                .Select(entry => new
                {
                    Entry = entry,
                    Similarity = _similarityService.Compute(queryEmbedding, entry.Embedding)
                })
                .OrderByDescending(x => x.Similarity.Aggregated)
                .Take(topK)
                .Select(x => new SearchResult(
                    x.Entry.Id,
                    x.Entry.Content,
                    x.Entry.Category,
                    x.Similarity.Aggregated))
                .ToList();

            foreach (var result in results)
            {
                logger.LogDebug("Non-Euclidean match {Id} -> score {Score:F4}", result.Id, result.Similarity);
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query: {Query}", query);
            return [];
        }
    }

    private async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var result = await embeddingGenerator.GenerateAsync([text]);
        return result.First().Vector.ToArray();
    }

    private static List<Document> GetMusicTheoryDocuments()
    {
        var docs = new List<Document>();

        // Generate ALL chord templates from the factory
        Console.WriteLine("[INFO] Generating chord templates from ChordTemplateFactory...");
        var allChordTemplates = ChordTemplateFactory.GenerateAllPossibleChords().ToList();
        Console.WriteLine($"[INFO] Generated {allChordTemplates.Count} chord templates");

        // Convert chord templates to documents
        // Group by unique pitch class set to avoid duplicates
        var uniqueChords = allChordTemplates
            .GroupBy(ct => ct.PitchClassSet.ToString())
            .Select(g => g.First())
            .ToList();

        Console.WriteLine($"[INFO] After deduplication: {uniqueChords.Count} unique chords");

        foreach (var template in uniqueChords.Take(1000)) // Limit to first 1000 to avoid overwhelming the system
        {
            var id = $"chord-{template.Name.ToLower().Replace(" ", "-").Replace("/", "-")}";
            var content = GenerateChordDescription(template);
            docs.Add(new Document(id, content, "Generated Chords"));
        }

        Console.WriteLine($"[INFO] Created {docs.Count} chord documents");

        // Add foundational music theory documents
        docs.AddRange(GetFoundationalTheoryDocuments());

        Console.WriteLine($"[INFO] Total documents: {docs.Count}");
        return docs;
    }

    private static string GenerateChordDescription(ChordTemplate template)
    {
        var description = $"{template.Name} is a {template.Quality} chord with {template.Extension} extension. ";
        description += $"It uses {template.StackingType} stacking. ";
        description += $"Contains {template.NoteCount} notes. ";

        // Add interval information
        var intervalDescriptions = template.Intervals.Select(i =>
        {
            if (i.Interval is Interval.Diatonic diatonic)
            {
                return diatonic.Name;
            }

            if (i.Interval is Interval.Chromatic chromatic)
            {
                return $"{chromatic.Semitones.Value} semitones";
            }

            return i.Interval.ToString();
        });
        description += $"Intervals: {string.Join(", ", intervalDescriptions)}. ";

        // Add context based on template type
        if (template is ChordTemplate.TonalModal tonalModal)
        {
            description +=
                $"This is the {tonalModal.HarmonicFunction} (degree {tonalModal.ScaleDegree}) in {tonalModal.ParentScale.Name}. ";
        }

        return description;
    }

    private static List<Document> GetFoundationalTheoryDocuments()
    {
        return
        [
            new("scale-major",
                "The major scale is a seven-note diatonic scale with the pattern: whole, whole, half, whole, whole, whole, half. It's the foundation of Western music theory and creates a bright, happy sound. Example: C major is C-D-E-F-G-A-B-C.",
                "Scales"),
            new("scale-minor",
                "Natural minor scales follow the pattern: whole, half, whole, whole, half, whole, whole. Harmonic minor raises the seventh degree. Melodic minor raises the sixth and seventh ascending. Minor scales create darker, more melancholic sounds.",
                "Scales"),
            new("modes-church",
                "The seven church modes are derived from the major scale: Ionian (major), Dorian (minor with raised 6th), Phrygian (minor with lowered 2nd), Lydian (major with raised 4th), Mixolydian (major with lowered 7th), Aeolian (natural minor), Locrian (diminished).",
                "Scales"),

            // Harmony and Progressions
            new("harmony-diatonic",
                "Diatonic harmony uses only notes from a single scale. In major keys: I and IV are major, ii, iii, and vi are minor, vii° is diminished. Common progressions include I-IV-V, I-vi-IV-V, and ii-V-I. These form the basis of most Western music.",
                "Harmony"),
            new("harmony-functional",
                "Functional harmony categorizes chords by their role: Tonic (I, vi) provides stability, Subdominant (IV, ii) creates movement away from tonic, Dominant (V, vii°) creates tension that resolves to tonic. This creates the tension and release that drives musical phrases.",
                "Harmony"),
            new("progression-jazz",
                "Jazz progressions often use ii-V-I cadences, tritone substitutions, and extended chords. The ii-V-I is the most common jazz progression. Modal jazz uses static harmony over modes. Bebop uses rapid chord changes and chromatic passing chords.",
                "Harmony"),

            // Guitar Techniques
            new("technique-barre",
                "Barre chords use one finger to press multiple strings, allowing moveable chord shapes. The E-shape and A-shape barre chords are most common. They enable playing any chord anywhere on the neck. Proper thumb position and finger pressure are essential for clean sound.",
                "Guitar"),
            new("technique-fingerstyle",
                "Fingerstyle guitar uses individual fingers to pluck strings instead of a pick. The thumb typically plays bass notes while fingers play melody and harmony. Patterns like Travis picking alternate bass notes with melody. This technique allows complex arrangements on solo guitar.",
                "Guitar"),

            // Music Theory Concepts
            new("theory-intervals",
                "Intervals measure the distance between two notes. Perfect intervals (unison, 4th, 5th, octave) sound stable. Major/minor intervals (2nd, 3rd, 6th, 7th) define chord quality. Augmented/diminished intervals create tension. Understanding intervals is fundamental to harmony and melody.",
                "Theory"),
            new("theory-circle-fifths",
                "The circle of fifths shows key relationships. Moving clockwise adds sharps (C-G-D-A-E-B-F#). Moving counter-clockwise adds flats (C-F-Bb-Eb-Ab-Db-Gb). Adjacent keys share many notes, making modulation smooth. It's essential for understanding key signatures and chord progressions.",
                "Theory")
        ];
    }

    private record Document(string Id, string Content, string Category);

    private record VectorEntry(string Id, string Content, string Category, float[] Embedding);
}

public record IndexingResult(bool Success, int DocumentsIndexed, TimeSpan Duration, string Message);

public record SearchResult(string Id, string Content, string Category, double Similarity);
