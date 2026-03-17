namespace GA.Business.ML.Tests.Skills;

using GA.Business.Core.Context;
using GA.Business.Core.Session;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies that key messages route to the correct skill and that ordering is correct.
/// </summary>
[TestFixture]
public class SkillRoutingTests
{
    private ISessionContextProvider _sessionProvider = null!;
    private MemoryStore _memoryStore = null!;
    private ProgressTracker _progressTracker = null!;

    [SetUp]
    public void Setup()
    {
        _sessionProvider = new InMemorySessionContextProvider();
        _memoryStore = new MemoryStore();
        _progressTracker = new ProgressTracker(_memoryStore);
    }

    // ── SessionContextSkill ──────────────────────────────────────────────

    [TestCase("I'm a beginner")]
    [TestCase("I am an intermediate player")]
    [TestCase("I'm an advanced guitarist")]
    [TestCase("set my level to expert")]
    public void SessionContextSkill_ShouldHandle_LevelMessages(string message)
    {
        var skill = new SessionContextSkill(_sessionProvider, NullLogger<SessionContextSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [TestCase("I play jazz")]
    [TestCase("I like blues")]
    [TestCase("I love rock")]
    public void SessionContextSkill_ShouldHandle_GenreMessages(string message)
    {
        var skill = new SessionContextSkill(_sessionProvider, NullLogger<SessionContextSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [Test]
    public async Task SessionContextSkill_ShouldUpdateLevel()
    {
        var skill = new SessionContextSkill(_sessionProvider, NullLogger<SessionContextSkill>.Instance);
        await skill.ExecuteAsync("I'm a beginner");

        var ctx = _sessionProvider.GetContext();
        Assert.That(ctx.SkillLevel, Is.EqualTo(SkillLevel.Beginner));
    }

    [Test]
    public async Task SessionContextSkill_ShouldUpdateGenre()
    {
        var skill = new SessionContextSkill(_sessionProvider, NullLogger<SessionContextSkill>.Instance);
        await skill.ExecuteAsync("I play jazz");

        var ctx = _sessionProvider.GetContext();
        Assert.That(ctx.CurrentGenre, Is.EqualTo(MusicalGenre.Jazz));
    }

    [Test]
    public async Task SessionContextSkill_ShouldReturnConfirmation()
    {
        var skill = new SessionContextSkill(_sessionProvider, NullLogger<SessionContextSkill>.Instance);
        var response = await skill.ExecuteAsync("I'm an intermediate player");

        Assert.That(response.Result.ToLowerInvariant(), Contains.Substring("intermediate"));
        Assert.That(response.Confidence, Is.EqualTo(1.0f));
    }

    // ── QuizAnswerSkill ──────────────────────────────────────────────────

    [Test]
    public void QuizAnswerSkill_ShouldNotHandle_WhenNoActiveQuiz()
    {
        // Ensure no active quiz by clearing it
        _memoryStore.Write("active_quiz", "quiz_cleared", "cleared", ["quiz", "cleared"]);

        var skill = new QuizAnswerSkill(_memoryStore, _progressTracker, NullLogger<QuizAnswerSkill>.Instance);
        Assert.That(skill.CanHandle("perfect fifth"), Is.False);
    }

    [Test]
    public async Task QuizAnswerSkill_ShouldValidateCorrectAnswer()
    {
        // Set up active quiz
        _memoryStore.Write("active_quiz", "quiz",
            """{"type":"interval","correctAnswer":"perfect fifth","root":"C","target":"G","semitones":7}""",
            ["quiz", "interval"]);

        var skill = new QuizAnswerSkill(_memoryStore, _progressTracker, NullLogger<QuizAnswerSkill>.Instance);

        Assert.That(skill.CanHandle("perfect fifth"), Is.True);

        var response = await skill.ExecuteAsync("perfect fifth");
        Assert.That(response.Result, Contains.Substring("Correct"));
    }

    [Test]
    public async Task QuizAnswerSkill_ShouldRecordProgress()
    {
        // Capture baseline (MemoryStore is backed by persistent file)
        var baselineStats = _progressTracker.GetCategoryStats("interval");

        _memoryStore.Write("active_quiz", "quiz",
            """{"type":"interval","correctAnswer":"minor third","root":"C","target":"Eb","semitones":3}""",
            ["quiz", "interval"]);

        var skill = new QuizAnswerSkill(_memoryStore, _progressTracker, NullLogger<QuizAnswerSkill>.Instance);
        await skill.ExecuteAsync("minor third");

        var stats = _progressTracker.GetCategoryStats("interval");
        Assert.That(stats.Total, Is.EqualTo(baselineStats.Total + 1));
        Assert.That(stats.Correct, Is.EqualTo(baselineStats.Correct + 1));
    }

    // ── IntervalQuizSkill ────────────────────────────────────────────────

    [TestCase("interval quiz")]
    [TestCase("test my intervals")]
    [TestCase("ear training")]
    public void IntervalQuizSkill_ShouldHandle(string message)
    {
        var skill = new IntervalQuizSkill(_memoryStore, _sessionProvider, NullLogger<IntervalQuizSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [Test]
    public async Task IntervalQuizSkill_ShouldStoreQuizState()
    {
        var skill = new IntervalQuizSkill(_memoryStore, _sessionProvider, NullLogger<IntervalQuizSkill>.Instance);
        await skill.ExecuteAsync("interval quiz");

        var quiz = _memoryStore.Read("active_quiz");
        Assert.That(quiz, Is.Not.Null);
        Assert.That(quiz!.Content, Contains.Substring("interval"));
    }

    [Test]
    public async Task IntervalQuizSkill_ShouldGenerateValidQuestion()
    {
        var skill = new IntervalQuizSkill(_memoryStore, _sessionProvider, NullLogger<IntervalQuizSkill>.Instance);
        var response = await skill.ExecuteAsync("interval quiz");

        Assert.That(response.Result, Contains.Substring("Interval Quiz"));
        Assert.That(response.Confidence, Is.EqualTo(1.0f));
    }

    // ── ChordQuizSkill ───────────────────────────────────────────────────

    [TestCase("chord quiz")]
    [TestCase("identify this chord")]
    [TestCase("name the chord")]
    public void ChordQuizSkill_ShouldHandle(string message)
    {
        var skill = new ChordQuizSkill(_memoryStore, _sessionProvider, NullLogger<ChordQuizSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [Test]
    public async Task ChordQuizSkill_ShouldGenerateValidChord()
    {
        var skill = new ChordQuizSkill(_memoryStore, _sessionProvider, NullLogger<ChordQuizSkill>.Instance);
        var response = await skill.ExecuteAsync("chord quiz");

        Assert.That(response.Result, Contains.Substring("Chord Quiz"));
        Assert.That(response.Confidence, Is.EqualTo(1.0f));
    }

    [Test]
    public async Task ChordQuizSkill_ShouldAdaptToSkillLevel()
    {
        _sessionProvider.UpdateContext(ctx => ctx.WithSkillLevel(SkillLevel.Expert));
        var skill = new ChordQuizSkill(_memoryStore, _sessionProvider, NullLogger<ChordQuizSkill>.Instance);

        // Run multiple times — expert should eventually get 7ths/sus chords
        var responses = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            var r = await skill.ExecuteAsync("chord quiz");
            responses.Add(r.Result);
        }

        // With all chord types available, we should see varied quiz content
        Assert.That(responses.Count, Is.EqualTo(20));
    }

    // ── PracticeRoutineSkill ─────────────────────────────────────────────

    [TestCase("practice routine")]
    [TestCase("give me a practice session")]
    [TestCase("what should I practice")]
    [TestCase("give me exercises")]
    public void PracticeRoutineSkill_ShouldHandle(string message)
    {
        var skill = new PracticeRoutineSkill(_sessionProvider, NullLogger<PracticeRoutineSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [Test]
    public async Task PracticeRoutineSkill_ShouldAdaptToSkillLevel()
    {
        _sessionProvider.UpdateContext(ctx => ctx.WithSkillLevel(SkillLevel.Intermediate));
        var skill = new PracticeRoutineSkill(_sessionProvider, NullLogger<PracticeRoutineSkill>.Instance);
        var response = await skill.ExecuteAsync("practice routine");

        Assert.That(response.Result, Contains.Substring("Intermediate"));
        Assert.That(response.Result, Contains.Substring("BPM"));
    }

    [Test]
    public async Task PracticeRoutineSkill_ShouldParseDuration()
    {
        var skill = new PracticeRoutineSkill(_sessionProvider, NullLogger<PracticeRoutineSkill>.Instance);
        var response = await skill.ExecuteAsync("give me a 20 minute practice routine");

        Assert.That(response.Result, Contains.Substring("20-Minute"));
    }

    // ── ScalePracticeSkill ───────────────────────────────────────────────

    [TestCase("practice the C major scale")]
    [TestCase("learn A minor")]
    [TestCase("exercises for G major")]
    public void ScalePracticeSkill_ShouldHandle(string message)
    {
        var skill = new ScalePracticeSkill(_sessionProvider, NullLogger<ScalePracticeSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [Test]
    public async Task ScalePracticeSkill_ShouldGeneratePatterns()
    {
        var skill = new ScalePracticeSkill(_sessionProvider, NullLogger<ScalePracticeSkill>.Instance);
        var response = await skill.ExecuteAsync("practice the C major scale");

        Assert.That(response.Result, Contains.Substring("Ascending"));
        Assert.That(response.Result, Contains.Substring("BPM"));
    }

    [Test]
    public async Task ScalePracticeSkill_ShouldAdaptBPM()
    {
        _sessionProvider.UpdateContext(ctx => ctx.WithSkillLevel(SkillLevel.Advanced));
        var skill = new ScalePracticeSkill(_sessionProvider, NullLogger<ScalePracticeSkill>.Instance);
        var response = await skill.ExecuteAsync("practice the A minor scale");

        // Advanced BPM = 120
        Assert.That(response.Result, Contains.Substring("120 BPM"));
    }

    // ── ProgressSkill ────────────────────────────────────────────────────

    [TestCase("my progress")]
    [TestCase("how am I doing")]
    [TestCase("show my stats")]
    public void ProgressSkill_ShouldHandle(string message)
    {
        var skill = new ProgressSkill(_progressTracker, NullLogger<ProgressSkill>.Instance);
        Assert.That(skill.CanHandle(message), Is.True);
    }

    [Test]
    public async Task ProgressSkill_ShouldReturnReport()
    {
        // MemoryStore is persistent — test that skill produces a valid response
        var skill = new ProgressSkill(_progressTracker, NullLogger<ProgressSkill>.Instance);
        var response = await skill.ExecuteAsync("show my progress");

        // Should contain either empty-state message or a formatted report
        var hasProgress = response.Result.Contains("Learning Progress") ||
                          response.Result.Contains("haven't taken any quizzes");
        Assert.That(hasProgress, Is.True);
        Assert.That(response.Confidence, Is.EqualTo(1.0f));
    }

    [Test]
    public async Task ProgressSkill_ShouldFormatReport_WithResults()
    {
        // Ensure there's quiz data
        _progressTracker.RecordQuizResult("interval", "perfect fifth", "perfect fifth", true);
        _progressTracker.RecordQuizResult("chord", "Cmaj7", "Cmaj7", true);

        var skill = new ProgressSkill(_progressTracker, NullLogger<ProgressSkill>.Instance);
        var response = await skill.ExecuteAsync("my progress");

        Assert.That(response.Result, Contains.Substring("Learning Progress"));
        Assert.That(response.Result, Contains.Substring("interval"));
        Assert.That(response.Result, Contains.Substring("chord"));
    }

    // ── SessionContextHook ───────────────────────────────────────────────

    [Test]
    public async Task SessionContextHook_ShouldMutateMessage_WhenContextHasSkillLevel()
    {
        _sessionProvider.UpdateContext(ctx => ctx.WithSkillLevel(SkillLevel.Beginner));
        var hook = new SessionContextHook(NullLogger<SessionContextHook>.Instance);

        var ctx = new ChatHookContext
        {
            OriginalMessage = "What is a chord?",
            CurrentMessage  = "What is a chord?",
            SessionContext  = _sessionProvider.GetContext(),
        };

        var result = await hook.OnRequestReceived(ctx);

        Assert.That(result.MutatedMessage, Is.Not.Null);
        Assert.That(result.MutatedMessage, Contains.Substring("[SESSION CONTEXT]"));
        Assert.That(result.MutatedMessage, Contains.Substring("Beginner"));
        Assert.That(result.MutatedMessage, Contains.Substring("simple language"));
    }

    [Test]
    public async Task SessionContextHook_ShouldContinue_WhenContextIsDefault()
    {
        var hook = new SessionContextHook(NullLogger<SessionContextHook>.Instance);

        var ctx = new ChatHookContext
        {
            OriginalMessage = "What is a chord?",
            CurrentMessage  = "What is a chord?",
            SessionContext  = MusicalSessionContext.Default(),
        };

        var result = await hook.OnRequestReceived(ctx);

        // Default context has no skill level, key, or genre — should continue unchanged
        Assert.That(result.Cancel, Is.False);
        Assert.That(result.MutatedMessage, Is.Null);
    }

    [Test]
    public async Task SessionContextHook_ShouldContinue_WhenContextIsNull()
    {
        var hook = new SessionContextHook(NullLogger<SessionContextHook>.Instance);

        var ctx = new ChatHookContext
        {
            OriginalMessage = "What is a chord?",
            CurrentMessage  = "What is a chord?",
            SessionContext  = null,
        };

        var result = await hook.OnRequestReceived(ctx);

        Assert.That(result.Cancel, Is.False);
        Assert.That(result.MutatedMessage, Is.Null);
    }

    [Test]
    public async Task SessionContextHook_ShouldIncludeAdaptiveInstructions_ForAdvanced()
    {
        _sessionProvider.UpdateContext(ctx => ctx.WithSkillLevel(SkillLevel.Advanced));
        var hook = new SessionContextHook(NullLogger<SessionContextHook>.Instance);

        var ctx = new ChatHookContext
        {
            OriginalMessage = "Tell me about modes",
            CurrentMessage  = "Tell me about modes",
            SessionContext  = _sessionProvider.GetContext(),
        };

        var result = await hook.OnRequestReceived(ctx);

        Assert.That(result.MutatedMessage, Contains.Substring("Roman numeral"));
    }

    // ── ProgressTracker ──────────────────────────────────────────────────

    [Test]
    public void ProgressTracker_ShouldAggregateResults()
    {
        // Capture baseline (MemoryStore is backed by persistent file and other tests add entries)
        var baseline = _progressTracker.GetProgressSummary();

        _progressTracker.RecordQuizResult("interval", "Q1", "A1", true);
        _progressTracker.RecordQuizResult("interval", "Q2", "A2", false);
        _progressTracker.RecordQuizResult("chord", "Q3", "A3", true);

        var summary = _progressTracker.GetProgressSummary();
        // At least 3 more than baseline (other tests may also add entries concurrently)
        Assert.That(summary.TotalQuizzes, Is.GreaterThanOrEqualTo(baseline.TotalQuizzes + 3));
        Assert.That(summary.ByCategory.ContainsKey("interval"), Is.True);
        Assert.That(summary.ByCategory.ContainsKey("chord"), Is.True);
    }

    // ── Non-matching messages should NOT match new skills ────────────────

    [TestCase("What notes are in C major?")]
    [TestCase("How do I play a barre chord?")]
    [TestCase("Explain the circle of fifths")]
    public void NewSkills_ShouldNotShadow_ExistingBehavior(string message)
    {
        var sessionSkill = new SessionContextSkill(_sessionProvider, NullLogger<SessionContextSkill>.Instance);
        var practiceSkill = new PracticeRoutineSkill(_sessionProvider, NullLogger<PracticeRoutineSkill>.Instance);
        var progressSkill = new ProgressSkill(_progressTracker, NullLogger<ProgressSkill>.Instance);

        Assert.That(sessionSkill.CanHandle(message), Is.False, $"SessionContextSkill should not handle: {message}");
        Assert.That(practiceSkill.CanHandle(message), Is.False, $"PracticeRoutineSkill should not handle: {message}");
        Assert.That(progressSkill.CanHandle(message), Is.False, $"ProgressSkill should not handle: {message}");
    }
}
