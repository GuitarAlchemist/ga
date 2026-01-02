namespace PerformanceOptimizationDemo;

using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
///     Demonstration of performance optimization techniques for Guitar Alchemist
/// </summary>
internal class Program
{
    private static async Task Main(string[] args)
    {
        // Create host with logging
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services => { services.AddLogging(builder => builder.AddConsole()); })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("🚀 Guitar Alchemist - Performance Optimization Demo");
        logger.LogInformation("=" + new string('=', 60));

        try
        {
            // Demo 1: Channels for High-Throughput Processing
            await DemoChannelsAsync(logger);

            // Demo 2: TPL Dataflow for Complex Pipelines
            await DemoDataflowAsync(logger);

            // Demo 3: Reactive Extensions for Real-time Processing
            await DemoReactiveAsync(logger);

            // Demo 4: Performance Comparison
            await DemoPerformanceComparisonAsync(logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo failed");
        }

        logger.LogInformation("Performance optimization demo completed! 🎉");
    }

    private static async Task DemoChannelsAsync(ILogger logger)
    {
        logger.LogInformation("\n📡 Channels Demo - High-Throughput Processing");
        logger.LogInformation("-" + new string('-', 45));

        // Create a bounded channel for musical data processing
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        var channel = Channel.CreateBounded<MusicalData>(options);
        var writer = channel.Writer;
        var reader = channel.Reader;

        // Producer task - generates musical data
        var producerTask = Task.Run(async () =>
        {
            for (var i = 0; i < 10000; i++)
            {
                var data = new MusicalData($"Note_{i}", i % 12, DateTime.UtcNow);
                await writer.WriteAsync(data);

                if (i % 1000 == 0)
                {
                    logger.LogInformation("Produced {Count} musical data items", i);
                }
            }

            writer.Complete();
        });

        // Consumer tasks - process musical data in parallel
        var consumerTasks = Enumerable.Range(0, Environment.ProcessorCount)
            .Select(id => Task.Run(async () =>
            {
                var processed = 0;
                await foreach (var data in reader.ReadAllAsync())
                {
                    // Simulate musical processing
                    await ProcessMusicalDataAsync(data);
                    processed++;
                }

                logger.LogInformation("Consumer {Id} processed {Count} items", id, processed);
            }))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(producerTask);
        await Task.WhenAll(consumerTasks);
        stopwatch.Stop();

        logger.LogInformation("Channels: Processed 10,000 items in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        logger.LogInformation("Throughput: {Throughput:F0} items/second", 10000.0 / stopwatch.Elapsed.TotalSeconds);
    }

    private static async Task DemoDataflowAsync(ILogger logger)
    {
        logger.LogInformation("\n🔄 TPL Dataflow Demo - Complex Processing Pipeline");
        logger.LogInformation("-" + new string('-', 50));

        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            BoundedCapacity = 100
        };

        // Stage 1: Parse musical input
        var parseBlock = new TransformBlock<string, MusicalData>(
            input => ParseMusicalInput(input), options);

        // Stage 2: Analyze harmony
        var analyzeBlock = new TransformBlock<MusicalData, AnalyzedData>(
            data => AnalyzeHarmony(data), options);

        // Stage 3: Generate recommendations
        var recommendBlock = new TransformBlock<AnalyzedData, RecommendationData>(
            analyzed => GenerateRecommendations(analyzed), options);

        // Link the pipeline
        parseBlock.LinkTo(analyzeBlock, new DataflowLinkOptions { PropagateCompletion = true });
        analyzeBlock.LinkTo(recommendBlock, new DataflowLinkOptions { PropagateCompletion = true });

        var stopwatch = Stopwatch.StartNew();

        // Send data through the pipeline
        var inputTask = Task.Run(async () =>
        {
            for (var i = 0; i < 1000; i++)
            {
                await parseBlock.SendAsync($"musical_input_{i}");
            }

            parseBlock.Complete();
        });

        // Collect results
        var results = new List<RecommendationData>();
        var outputTask = Task.Run(async () =>
        {
            while (await recommendBlock.OutputAvailableAsync())
            {
                var result = await recommendBlock.ReceiveAsync();
                results.Add(result);
            }
        });

        await inputTask;
        await recommendBlock.Completion;
        stopwatch.Stop();

        logger.LogInformation("Dataflow: Processed {Count} items through 3-stage pipeline in {ElapsedMs}ms",
            results.Count, stopwatch.ElapsedMilliseconds);
        logger.LogInformation("Pipeline throughput: {Throughput:F0} items/second",
            results.Count / stopwatch.Elapsed.TotalSeconds);
    }

    private static async Task DemoReactiveAsync(ILogger logger)
    {
        logger.LogInformation("\n⚡ Reactive Extensions Demo - Real-time Processing");
        logger.LogInformation("-" + new string('-', 50));

        var subject = new Subject<MusicalEvent>();
        var stopwatch = Stopwatch.StartNew();
        var processedCount = 0;

        // Create reactive pipeline
        var subscription = subject
            .Buffer(TimeSpan.FromMilliseconds(100)) // Batch events every 100ms
            .Where(batch => batch.Count > 0)
            .SelectMany(batch => ProcessBatchAsync(batch))
            .Subscribe(
                _ =>
                {
                    Interlocked.Increment(ref processedCount);
                    if (processedCount % 100 == 0)
                    {
                        logger.LogInformation("Processed {Count} musical events", processedCount);
                    }
                },
                error => logger.LogError(error, "Error in reactive pipeline"),
                () => logger.LogInformation("Reactive pipeline completed"));

        // Generate real-time musical events
        var eventTask = Task.Run(async () =>
        {
            for (var i = 0; i < 1000; i++)
            {
                var musicalEvent = new MusicalEvent($"Event_{i}", i % 12, DateTime.UtcNow);
                subject.OnNext(musicalEvent);

                // Simulate real-time arrival
                await Task.Delay(1);
            }

            subject.OnCompleted();
        });

        await eventTask;
        await Task.Delay(500); // Allow final batches to process
        stopwatch.Stop();

        subscription.Dispose();

        logger.LogInformation("Reactive: Processed {Count} events in {ElapsedMs}ms",
            processedCount, stopwatch.ElapsedMilliseconds);
        logger.LogInformation("Event throughput: {Throughput:F0} events/second",
            processedCount / stopwatch.Elapsed.TotalSeconds);
    }

    private static async Task DemoPerformanceComparisonAsync(ILogger logger)
    {
        logger.LogInformation("\n📊 Performance Comparison - Sequential vs Parallel");
        logger.LogInformation("-" + new string('-', 55));

        const int itemCount = 1000;
        var data = Enumerable.Range(0, itemCount)
            .Select(i => new MusicalData($"Item_{i}", i % 12, DateTime.UtcNow))
            .ToList();

        // Sequential processing
        var sequentialStopwatch = Stopwatch.StartNew();
        var sequentialResults = new List<ProcessedData>();
        foreach (var item in data)
        {
            var result = await ProcessMusicalDataAsync(item);
            sequentialResults.Add(result);
        }

        sequentialStopwatch.Stop();

        // Parallel processing
        var parallelStopwatch = Stopwatch.StartNew();
        var parallelResults = await Task.WhenAll(
            data.Select(ProcessMusicalDataAsync));
        parallelStopwatch.Stop();

        logger.LogInformation("Sequential: {Count} items in {ElapsedMs}ms ({Throughput:F0} items/sec)",
            itemCount, sequentialStopwatch.ElapsedMilliseconds,
            itemCount / sequentialStopwatch.Elapsed.TotalSeconds);

        logger.LogInformation("Parallel: {Count} items in {ElapsedMs}ms ({Throughput:F0} items/sec)",
            itemCount, parallelStopwatch.ElapsedMilliseconds,
            itemCount / parallelStopwatch.Elapsed.TotalSeconds);

        var speedup = (double)sequentialStopwatch.ElapsedMilliseconds / parallelStopwatch.ElapsedMilliseconds;
        logger.LogInformation("Speedup: {Speedup:F2}x faster with parallel processing", speedup);
    }

    // Helper methods and data types
    private static async Task<ProcessedData> ProcessMusicalDataAsync(MusicalData data)
    {
        // Simulate musical processing work
        await Task.Delay(1);
        return new ProcessedData(data.Name, data.PitchClass, data.Timestamp, "Processed");
    }

    private static MusicalData ParseMusicalInput(string input)
    {
        var parts = input.Split('_');
        var id = int.Parse(parts[1]);
        return new MusicalData(input, id % 12, DateTime.UtcNow);
    }

    private static AnalyzedData AnalyzeHarmony(MusicalData data)
    {
        // Simulate harmony analysis
        var harmony = data.PitchClass switch
        {
            0 => "C Major",
            4 => "E Major",
            7 => "G Major",
            _ => "Unknown"
        };
        return new AnalyzedData(data, harmony, 0.8);
    }

    private static RecommendationData GenerateRecommendations(AnalyzedData analyzed)
    {
        var recommendations = new[] { "Practice scales", "Work on chord progressions", "Study voice leading" };
        return new RecommendationData(analyzed, recommendations);
    }

    private static async Task<IEnumerable<ProcessedEvent>> ProcessBatchAsync(IList<MusicalEvent> events)
    {
        await Task.Delay(10); // Simulate batch processing
        return events.Select(e => new ProcessedEvent(e.Name, e.PitchClass, "Batch processed"));
    }
}

// Data types
internal record MusicalData(string Name, int PitchClass, DateTime Timestamp);

internal record ProcessedData(string Name, int PitchClass, DateTime Timestamp, string Status);

internal record AnalyzedData(MusicalData Original, string Harmony, double Confidence);

internal record RecommendationData(AnalyzedData Analysis, string[] Recommendations);

internal record MusicalEvent(string Name, int PitchClass, DateTime Timestamp);

internal record ProcessedEvent(string Name, int PitchClass, string Status);
