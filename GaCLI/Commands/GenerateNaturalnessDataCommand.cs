namespace GaCLI.Commands;

using GA.Domain.Core.Instruments;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Naturalness;

public class GenerateNaturalnessDataCommand
{
    private readonly NaturalnessTrainingDataGenerator _generator;
    private readonly GA.Domain.Repositories.ITabCorpusRepository _repository;

    public GenerateNaturalnessDataCommand(NaturalnessTrainingDataGenerator generator, GA.Domain.Repositories.ITabCorpusRepository repository)
    {
        _generator = generator;
        _repository = repository;
    }

    public async Task ExecuteAsync(string outputPath = "naturalness_data.csv", int limit = 1000)
    {
        // Check if we have data
        var all = await _repository.GetAllAsync();
        if (!all.Any(x => x.Format == "ASCII"))
        {
            Console.WriteLine("No ASCII tabs found in corpus. Seeding test data...");
            await SeedDataAsync();
        }

        Console.WriteLine($"Genering naturalness training data (Limit: {limit})...");
        
        var csvContent = await _generator.GenerateCsvAsync(limit);
        
        await File.WriteAllTextAsync(outputPath, csvContent);
        
        Console.WriteLine($"Successfully generated {csvContent.Split('\n').Length - 1} rows.");
        Console.WriteLine($"Saved to: {Path.GetFullPath(outputPath)}");
    }

    private async Task SeedDataAsync()
    {
        var tab = @"
Tuning: E A D G B E
C Major Scale
e|---------------------------------|
B|---------------------------------|
G|-----------------0-2-4-5---------|
D|-----------0-2-3-----------------|
A|-----0-2-3-----------------------|
E|-3-5-----------------------------|

Simple Chords
    C   Am  F   G
e|--0---0---1---3---|
B|--1---1---1---0---|
G|--0---2---2---0---|
D|--2---2---3---0---|
A|--3---0---3---2---|
E|----------1---3---|
";
        await _repository.SaveAsync(new GA.Domain.Core.Theory.Tabs.TabCorpusItem 
        {
            Id = "111111111111111111111111", // valid hex id
            SourceId = "dev-seed",
            ExternalId = "test-tab-1",
            Content = tab,
            Format = "ASCII"
        });
        Console.WriteLine("Seeded 'test-tab-1'.");
    }
}