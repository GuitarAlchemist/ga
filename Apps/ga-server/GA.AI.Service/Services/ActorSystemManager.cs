namespace GA.AI.Service.Services;

using Models;

public class ActorSystemManager(ILogger<ActorSystemManager> logger)
{
    public Task<T> AskPlayerSession<T>(string playerId, object message)
    {
        logger.LogInformation("Asking player session {PlayerId} with message {MessageType}", playerId,
            message.GetType().Name);

        // Mock implementation
        if (typeof(T) == typeof(DifficultyResponse))
        {
            return Task.FromResult((T)(object)new DifficultyResponse
            {
                CurrentDifficulty = 0.5,
                RecommendedDifficulty = 0.55,
                Reason = "Mock response"
            });
        }

        if (typeof(T) == typeof(SessionStateResponse))
        {
            return Task.FromResult((T)(object)new SessionStateResponse
            {
                SessionId = playerId,
                State = "Active",
                Progress = 0.5
            });
        }

        return Task.FromResult<T>(default);
    }

    public async Task StopPlayerSession(string playerId)
    {
        logger.LogInformation("Stopping player session {PlayerId}", playerId);
        await Task.CompletedTask;
    }
}
