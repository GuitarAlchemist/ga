namespace GaCLI.Commands;

using System;
using System.Threading.Tasks;
using GA.Business.ML.Tabs;

public class IngestCorpusCommand
{
    private readonly TabCorpusService _service;

    public IngestCorpusCommand(TabCorpusService service)
    {
        _service = service;
    }

    public async Task ExecuteAsync()
    {
        Console.WriteLine("Starting Corpus Ingestion...");
        await _service.IngestAllConfiguredSourcesAsync();
        
        var count = await _service.GetCorpusSizeAsync();
        Console.WriteLine($"\nIngestion Complete. Total Items: {count}");
    }
}
