namespace GA.AI.Service.Services;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GA.AI.Service.Models;

public class ActorSystemManager
{
    private readonly ILogger<ActorSystemManager> _logger;

    public ActorSystemManager(ILogger<ActorSystemManager> logger)
    {
        _logger = logger;
    }

    public async Task<T> AskPlayerSession<T>(string playerId, object message)
    {
        _logger.LogInformation("Asking player session {PlayerId} with message {MessageType}", playerId, message.GetType().Name);
        
        // Mock implementation
        if (typeof(T) == typeof(DifficultyResponse))
        {
            return (T)(object)new DifficultyResponse
            {
                CurrentDifficulty = 0.5,
                RecommendedDifficulty = 0.55,
                Reason = "Mock response"
            };
        }
        
        if (typeof(T) == typeof(SessionStateResponse))
        {
             return (T)(object)new SessionStateResponse
             {
                 SessionId = playerId,
                 State = "Active",
                 Progress = 0.5
             };
        }

        return default;
    }

    public async Task StopPlayerSession(string playerId)
    {
        _logger.LogInformation("Stopping player session {PlayerId}", playerId);
        await Task.CompletedTask;
    }
}
