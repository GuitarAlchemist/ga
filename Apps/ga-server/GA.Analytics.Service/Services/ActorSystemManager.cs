namespace GA.Analytics.Service.Services;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class ActorSystemManager(ILogger<ActorSystemManager> logger)
{
    public async Task<T> AskPlayerSession<T>(string playerId, object message)
    {
        logger.LogInformation("Asking player session {PlayerId} with message {MessageType}", playerId, message.GetType().Name);
        await Task.Delay(50);
        return default;
    }

    public async Task StopPlayerSession(string playerId)
    {
        logger.LogInformation("Stopping player session {PlayerId}", playerId);
        await Task.CompletedTask;
    }
}
