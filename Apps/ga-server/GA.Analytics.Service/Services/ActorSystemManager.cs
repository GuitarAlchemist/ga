namespace GA.Analytics.Service.Services;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
        await Task.Delay(50);
        return default;
    }

    public async Task StopPlayerSession(string playerId)
    {
        _logger.LogInformation("Stopping player session {PlayerId}", playerId);
        await Task.CompletedTask;
    }
}
