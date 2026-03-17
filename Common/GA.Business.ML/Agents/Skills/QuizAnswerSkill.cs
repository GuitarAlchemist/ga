namespace GA.Business.ML.Agents.Skills;

using System.Text.Json;
using System.Text.RegularExpressions;
using GA.Business.ML.Agents.Memory;

/// <summary>
/// Validates answers to active quizzes (interval or chord) stored in <see cref="MemoryStore"/>.
/// Must be registered BEFORE quiz generation skills so answers take priority.
/// </summary>
public sealed class QuizAnswerSkill(
    MemoryStore memoryStore,
    ProgressTracker progressTracker,
    ILogger<QuizAnswerSkill> logger) : IOrchestratorSkill
{
    public string Name        => "QuizAnswer";
    public string Description => "Validates answers to active quizzes";

    private static readonly Regex AnswerPattern = new(
        @"\b(?:minor|major|perfect|diminished|augmented|tritone)\s*(?:second|third|fourth|fifth|sixth|seventh|unison|octave)?\b" +
        @"|\b(?:second|third|fourth|fifth|sixth|seventh|unison|octave)\b" +
        @"|\btritone\b" +
        @"|\b(?:maj|min|dim|aug|sus|dom)\d*\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message)
    {
        // Only handle if there's an active quiz (not cleared) AND the message looks like an answer
        var quiz = memoryStore.Read("active_quiz");
        return quiz is not null && quiz.Type == "quiz" && AnswerPattern.IsMatch(message);
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var quiz = memoryStore.Read("active_quiz");
        if (quiz is null)
            return Task.FromResult(new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = "There's no active quiz. Say \"interval quiz\" or \"chord quiz\" to start one!",
                Confidence = 0.5f,
                Evidence   = [],
                Assumptions = []
            });

        try
        {
            var doc = JsonDocument.Parse(quiz.Content);
            var quizType = doc.RootElement.GetProperty("type").GetString() ?? "unknown";
            var correctAnswer = doc.RootElement.GetProperty("correctAnswer").GetString() ?? "";

            var userAnswer = NormalizeAnswer(message);
            var normalizedCorrect = NormalizeAnswer(correctAnswer);
            var isCorrect = string.Equals(userAnswer, normalizedCorrect, StringComparison.OrdinalIgnoreCase)
                         || userAnswer.Contains(normalizedCorrect, StringComparison.OrdinalIgnoreCase)
                         || normalizedCorrect.Contains(userAnswer, StringComparison.OrdinalIgnoreCase);

            // Record progress
            progressTracker.RecordQuizResult(quizType, correctAnswer, message, isCorrect);

            // Clear quiz state
            memoryStore.Write("active_quiz_completed", "quiz", quiz.Content, ["quiz", "completed"]);
            // Remove active quiz by overwriting with empty
            ClearActiveQuiz();

            logger.LogDebug("QuizAnswerSkill: answer={Answer}, correct={Correct}, expected={Expected}",
                message, isCorrect, correctAnswer);

            var response = isCorrect
                ? $"**Correct!** The answer is **{correctAnswer}**. Well done!\n\nWant another quiz? Just say \"interval quiz\" or \"chord quiz\"."
                : $"**Not quite.** The correct answer is **{correctAnswer}**.\n\nWant to try another? Say \"interval quiz\" or \"chord quiz\".";

            // Add context for interval quizzes
            if (quizType == "interval" && doc.RootElement.TryGetProperty("root", out var root)
                                       && doc.RootElement.TryGetProperty("target", out var target))
            {
                response = isCorrect
                    ? $"**Correct!** {root.GetString()} to {target.GetString()} is a **{correctAnswer}**. Well done!\n\nWant another? Say \"interval quiz\"."
                    : $"**Not quite.** {root.GetString()} to {target.GetString()} is a **{correctAnswer}**.\n\nWant to try another? Say \"interval quiz\".";
            }
            // Add context for chord quizzes
            else if (quizType == "chord" && doc.RootElement.TryGetProperty("notes", out var notes))
            {
                response = isCorrect
                    ? $"**Correct!** Those notes form a **{correctAnswer}**. Well done!\n\nWant another? Say \"chord quiz\"."
                    : $"**Not quite.** Those notes form a **{correctAnswer}**.\n\nWant to try another? Say \"chord quiz\".";
            }

            return Task.FromResult(new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = response,
                Confidence = 1.0f,
                Evidence   = [$"Expected: {correctAnswer}", $"User answer: {message}", $"Correct: {isCorrect}"],
                Assumptions = []
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "QuizAnswerSkill: failed to parse quiz state");
            ClearActiveQuiz();
            return Task.FromResult(new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = "Something went wrong with the quiz. Let's start fresh — say \"interval quiz\" or \"chord quiz\".",
                Confidence = 0.5f,
                Evidence   = [],
                Assumptions = ["Quiz state was corrupted"]
            });
        }
    }

    private void ClearActiveQuiz()
    {
        // Overwrite with a cleared state
        memoryStore.Write("active_quiz", "quiz_cleared", "cleared", ["quiz", "cleared"]);
    }

    private static string NormalizeAnswer(string answer)
    {
        return Regex.Replace(answer.ToLowerInvariant().Trim(), @"\s+", " ")
            .Replace("p4", "perfect fourth")
            .Replace("p5", "perfect fifth")
            .Replace("m2", "minor second")
            .Replace("m3", "minor third")
            .Replace("m6", "minor sixth")
            .Replace("m7", "minor seventh");
    }
}
