namespace GaApi.Tests;

using Actors.Messages;
using Microsoft.Extensions.Logging;
using Services;

/// <summary>
///     Tests for the Proto.Actor system implementation
/// </summary>
[TestFixture]
[Category("ActorSystem")]
public class ActorSystemTests
{
    [SetUp]
    public void Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = _loggerFactory.CreateLogger<ActorSystemManager>();
        var orchestrator = CreateMockOrchestrator();
        _actorSystem = new ActorSystemManager(_loggerFactory, orchestrator, logger);
    }

    [TearDown]
    public void TearDown()
    {
        _actorSystem?.Dispose();
        _loggerFactory?.Dispose();
    }

    private ActorSystemManager? _actorSystem;
    private ILoggerFactory? _loggerFactory;

    [Test]
    public async Task PlayerSessionActor_ShouldUpdatePerformance()
    {
        // Arrange
        var playerId = "test-player-1";
        var message = new UpdatePerformance(
            0.85,
            0.7,
            0.9,
            "C-major-scale"
        );

        // Act
        var response = await _actorSystem!.AskPlayerSession<DifficultyResponse>(playerId, message);

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.CurrentDifficulty, Is.GreaterThan(0));
        Assert.That(response.AdaptationReason, Does.Contain("Updated based on performance"));

        TestContext.Out.WriteLine($"Current Difficulty: {response.CurrentDifficulty:F2}");
        TestContext.Out.WriteLine($"Target Difficulty: {response.TargetDifficulty:F2}");
        TestContext.Out.WriteLine($"Adaptation Reason: {response.AdaptationReason}");
    }

    [Test]
    public async Task PlayerSessionActor_ShouldGetDifficulty()
    {
        // Arrange
        var playerId = "test-player-2";

        // Act
        var response = await _actorSystem!.AskPlayerSession<DifficultyResponse>(playerId, new GetDifficulty());

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.CurrentDifficulty, Is.GreaterThanOrEqualTo(0));

        TestContext.Out.WriteLine($"Initial Difficulty: {response.CurrentDifficulty:F2}");
    }

    [Test]
    public async Task PlayerSessionActor_ShouldGetRecommendedExercise()
    {
        // Arrange
        var playerId = "test-player-3";

        // Act
        var response = await _actorSystem!.AskPlayerSession<ExerciseRecommendation>(
            playerId,
            new GetRecommendedExercise()
        );

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.ExerciseId, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Difficulty, Is.GreaterThanOrEqualTo(0));
        Assert.That(response.Parameters, Is.Not.Null);

        TestContext.Out.WriteLine($"Exercise ID: {response.ExerciseId}");
        TestContext.Out.WriteLine($"Difficulty: {response.Difficulty:F2}");
        TestContext.Out.WriteLine($"Rationale: {response.Rationale}");
    }

    [Test]
    public async Task PlayerSessionActor_ShouldGetSessionState()
    {
        // Arrange
        var playerId = "test-player-4";

        // First, record some performance
        await _actorSystem!.AskPlayerSession<DifficultyResponse>(
            playerId,
            new UpdatePerformance(0.8, 0.6, 0.85, "test-exercise")
        );

        // Act
        var response = await _actorSystem.AskPlayerSession<SessionStateResponse>(
            playerId,
            new GetSessionState()
        );

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.PlayerId, Is.EqualTo(playerId));
        Assert.That(response.TotalExercises, Is.GreaterThan(0));
        Assert.That(response.Metrics, Is.Not.Null);

        TestContext.Out.WriteLine($"Player ID: {response.PlayerId}");
        TestContext.Out.WriteLine($"Total Exercises: {response.TotalExercises}");
        TestContext.Out.WriteLine($"Session Duration: {response.SessionDuration.TotalSeconds:F1}s");
    }

    [Test]
    public async Task PlayerSessionActor_ShouldIsolatePlayerSessions()
    {
        // Arrange
        var player1 = "test-player-5";
        var player2 = "test-player-6";

        // Act - Update both players with different performance
        var response1 = await _actorSystem!.AskPlayerSession<DifficultyResponse>(
            player1,
            new UpdatePerformance(0.9, 0.8, 0.95, "high-performer")
        );

        var response2 = await _actorSystem.AskPlayerSession<DifficultyResponse>(
            player2,
            new UpdatePerformance(0.3, 0.2, 0.4, "low-performer")
        );

        // Assert - Players should have different difficulties
        Assert.That(response1.CurrentDifficulty, Is.Not.EqualTo(response2.CurrentDifficulty));

        TestContext.Out.WriteLine($"Player 1 Difficulty: {response1.CurrentDifficulty:F2}");
        TestContext.Out.WriteLine($"Player 2 Difficulty: {response2.CurrentDifficulty:F2}");
    }

    [Test]
    public async Task PlayerSessionActor_ShouldStopSession()
    {
        // Arrange
        var playerId = "test-player-7";

        // Create session
        await _actorSystem!.AskPlayerSession<DifficultyResponse>(
            playerId,
            new GetDifficulty()
        );

        // Act
        await _actorSystem.StopPlayerSession(playerId);

        // Assert - Should create new session on next access
        var response = await _actorSystem.AskPlayerSession<DifficultyResponse>(
            playerId,
            new GetDifficulty()
        );

        Assert.That(response, Is.Not.Null);
        TestContext.Out.WriteLine("Session successfully stopped and recreated");
    }

    [Test]
    public async Task ActorSystemManager_ShouldGetActivePlayerSessions()
    {
        // Arrange
        var player1 = "test-player-8";
        var player2 = "test-player-9";

        await _actorSystem!.AskPlayerSession<DifficultyResponse>(player1, new GetDifficulty());
        await _actorSystem.AskPlayerSession<DifficultyResponse>(player2, new GetDifficulty());

        // Act
        var activeSessions = _actorSystem.GetActivePlayerSessions();

        // Assert
        Assert.That(activeSessions, Is.Not.Null);
        Assert.That(activeSessions.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(activeSessions, Does.Contain(player1));
        Assert.That(activeSessions, Does.Contain(player2));

        TestContext.Out.WriteLine($"Active Sessions: {activeSessions.Count}");
        foreach (var playerId in activeSessions)
        {
            TestContext.Out.WriteLine($"  - {playerId}");
        }
    }

    private GuitarAgentOrchestrator CreateMockOrchestrator()
    {
        // Create a minimal mock orchestrator for testing
        // GuitarAgentOrchestrator uses primary constructor with:
        // IChatClient, IOptionsMonitor<GuitarAgentOptions>, ILoggerFactory, IServiceProvider, ILogger
        var logger = _loggerFactory!.CreateLogger<GuitarAgentOrchestrator>();

        // For unit tests, we just need a non-null instance
        // The actual AI agent task tests would require integration testing
        return new GuitarAgentOrchestrator(
            null!, // chatClient
            null!, // options
            _loggerFactory, // loggerFactory
            null!, // serviceProvider
            logger
        );
    }
}
