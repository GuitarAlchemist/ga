using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;

namespace GaCLI.Commands;

public class Runner(MusicalObjectsService syncService)
{
    public async Task<int> ExecuteAsync()
    {
        Console.WriteLine("Starting MongoDB sync...");
        
        var result = await syncService.SyncAllAsync();
        
        Console.WriteLine("Sync results:");
        foreach (var (type, count) in result.Counts)
        {
            Console.WriteLine($"- {type.Name}: {count}");
        }

        if (result.Errors.Any())
        {
            Console.WriteLine("\nErrors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"- {error}");
            }
            return 1;
        }

        Console.WriteLine("\nMongoDB sync completed successfully!");
        return 0;
    }
}