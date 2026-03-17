namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Displays the user's learning progress from quiz results.
/// </summary>
public sealed class ProgressSkill(
    ProgressTracker progressTracker,
    ILogger<ProgressSkill> logger) : IOrchestratorSkill
{
    public string Name        => "Progress";
    public string Description => "Shows learning progress and quiz statistics";

    private static readonly Regex ProgressPattern = new(
        @"\b(?:my\s+progress|how\s+am\s+I\s+doing|show\s+(?:my\s+)?stats|what\s+have\s+I\s+learned|quiz\s+(?:results|stats|score|history))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message) => ProgressPattern.IsMatch(message);

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var summary = progressTracker.GetProgressSummary();
        logger.LogDebug("ProgressSkill: total={Total}, correct={Correct}", summary.TotalQuizzes, summary.CorrectAnswers);

        if (summary.TotalQuizzes == 0)
            return Task.FromResult(new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = "You haven't taken any quizzes yet! Try saying:\n" +
                             "- \"**interval quiz**\" to test your interval recognition\n" +
                             "- \"**chord quiz**\" to test your chord identification",
                Confidence = 1.0f,
                Evidence   = [],
                Assumptions = []
            });

        var sb = new StringBuilder();
        sb.AppendLine("## Your Learning Progress");
        sb.AppendLine();
        sb.AppendLine($"**Total quizzes:** {summary.TotalQuizzes}");
        sb.AppendLine($"**Correct answers:** {summary.CorrectAnswers}/{summary.TotalQuizzes} ({summary.Accuracy:F0}%)");
        sb.AppendLine();

        if (summary.ByCategory.Count > 0)
        {
            sb.AppendLine("### By Category");
            sb.AppendLine();
            sb.AppendLine("| Category | Score | Accuracy |");
            sb.AppendLine("|----------|-------|----------|");
            foreach (var (category, stats) in summary.ByCategory)
            {
                var emoji = stats.Accuracy >= 80 ? "**" : "";
                sb.AppendLine($"| {category} | {emoji}{stats.Correct}/{stats.Total}{emoji} | {stats.Accuracy:F0}% |");
            }
            sb.AppendLine();
        }

        // Encouragement
        sb.AppendLine(summary.Accuracy switch
        {
            >= 90 => "Excellent work! You're really mastering these concepts.",
            >= 70 => "Good progress! Keep practicing to build consistency.",
            >= 50 => "You're getting there! Regular practice will help solidify these concepts.",
            _     => "Don't worry — learning takes time. Keep at it and you'll see improvement!"
        });

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   = [$"Total: {summary.TotalQuizzes}", $"Accuracy: {summary.Accuracy:F0}%"],
            Assumptions = []
        });
    }
}
