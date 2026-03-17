namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.Json;
using GA.Business.ML.Agents.Memory;

/// <summary>
/// Tracks quiz results and learning progress via <see cref="MemoryStore"/>.
/// </summary>
public sealed class ProgressTracker(MemoryStore memoryStore)
{
    /// <summary>
    /// Records a quiz result.
    /// </summary>
    private static long _counter;

    public void RecordQuizResult(string category, string question, string userAnswer, bool correct)
    {
        var seq = Interlocked.Increment(ref _counter);
        var key = $"progress_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{seq}";
        var content = JsonSerializer.Serialize(new
        {
            category,
            question,
            userAnswer,
            correct,
            timestamp = DateTimeOffset.UtcNow
        });
        memoryStore.Write(key, "progress", content, ["progress", category]);
    }

    /// <summary>
    /// Returns aggregate progress statistics.
    /// </summary>
    public ProgressSummary GetProgressSummary()
    {
        var entries = memoryStore.Search("progress", type: "progress");
        var results = entries.Select(ParseResult).Where(r => r is not null).ToList();

        var total   = results.Count;
        var correct = results.Count(r => r!.Value.correct);

        var byCategory = results
            .GroupBy(r => r!.Value.category)
            .ToDictionary(
                g => g.Key,
                g => new CategoryStats(g.Count(), g.Count(r => r!.Value.correct)));

        return new ProgressSummary(total, correct, byCategory);
    }

    /// <summary>
    /// Returns stats for a specific category.
    /// </summary>
    public CategoryStats GetCategoryStats(string category)
    {
        var summary = GetProgressSummary();
        return summary.ByCategory.GetValueOrDefault(category, new CategoryStats(0, 0));
    }

    private static (string category, bool correct)? ParseResult(MemoryEntry entry)
    {
        try
        {
            var doc = JsonDocument.Parse(entry.Content);
            var category = doc.RootElement.GetProperty("category").GetString() ?? "unknown";
            var correct  = doc.RootElement.GetProperty("correct").GetBoolean();
            return (category, correct);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record ProgressSummary(
    int TotalQuizzes,
    int CorrectAnswers,
    IReadOnlyDictionary<string, CategoryStats> ByCategory)
{
    public double Accuracy => TotalQuizzes > 0 ? (double)CorrectAnswers / TotalQuizzes * 100 : 0;
}

public sealed record CategoryStats(int Total, int Correct)
{
    public double Accuracy => Total > 0 ? (double)Correct / Total * 100 : 0;
}
