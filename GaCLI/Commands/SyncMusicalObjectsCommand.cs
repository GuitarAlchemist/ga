namespace GaCLI.Commands;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using Microsoft.Extensions.Logging;

public class SyncMusicalObjectsCommand
{
    private readonly IMusicalObjectsService _musicalObjectsService;
    private readonly ILogger<SyncMusicalObjectsCommand> _logger;

    public SyncMusicalObjectsCommand(
        IMusicalObjectsService musicalObjectsService,
        ILogger<SyncMusicalObjectsCommand> logger)
    {
        _musicalObjectsService = musicalObjectsService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting musical objects sync...");
        
        var result = await _musicalObjectsService.SyncAllAsync();
        
        if (result.Errors.Any())
        {
            _logger.LogError("Sync completed with errors:");
            foreach (var error in result.Errors)
            {
                _logger.LogError("- {Error}", error);
            }
        }
        
        _logger.LogInformation("Sync results:");
        _logger.LogInformation("- Notes added: {Count}", result.NotesAdded);
        _logger.LogInformation("- Intervals added: {Count}", result.IntervalsAdded);
        _logger.LogInformation("- Keys added: {Count}", result.KeysAdded);
        _logger.LogInformation("- Scales added: {Count}", result.ScalesAdded);
        _logger.LogInformation("- Pitch classes added: {Count}", result.PitchClassesAdded);
    }
}