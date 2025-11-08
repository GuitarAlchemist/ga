using System.Threading.Channels;

namespace GA.Business.Core.Performance;

/// <summary>
/// High-performance channel-based processor for musical data processing
/// </summary>
/// <typeparam name="TInput">Input data type</typeparam>
/// <typeparam name="TOutput">Output data type</typeparam>
[PublicAPI]
public sealed class ChannelBasedProcessor<TInput, TOutput> : IDisposable
{
    private readonly Channel<TInput> _inputChannel;
    private readonly Channel<TOutput> _outputChannel;
    private readonly Func<TInput, ValueTask<TOutput>> _processor;
    private readonly ILogger<ChannelBasedProcessor<TInput, TOutput>> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task[] _processingTasks;
    private readonly int _concurrency;
    private bool _disposed;

    public ChannelBasedProcessor(
        Func<TInput, ValueTask<TOutput>> processor,
        ILogger<ChannelBasedProcessor<TInput, TOutput>> logger,
        int concurrency = -1,
        int capacity = 1000)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _concurrency = concurrency == -1 ? Environment.ProcessorCount : concurrency;
        _cancellationTokenSource = new CancellationTokenSource();

        // Create bounded channels for backpressure
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _inputChannel = Channel.CreateBounded<TInput>(options);
        _outputChannel = Channel.CreateBounded<TOutput>(options);

        // Start processing tasks
        _processingTasks = new Task[_concurrency];
        for (int i = 0; i < _concurrency; i++)
        {
            _processingTasks[i] = ProcessAsync(_cancellationTokenSource.Token);
        }

        _logger.LogInformation("ChannelBasedProcessor started with {Concurrency} workers", _concurrency);
    }

    /// <summary>
    /// Gets the input writer for sending data to process
    /// </summary>
    public ChannelWriter<TInput> Input => _inputChannel.Writer;

    /// <summary>
    /// Gets the output reader for receiving processed data
    /// </summary>
    public ChannelReader<TOutput> Output => _outputChannel.Reader;

    /// <summary>
    /// Processes a single item through the pipeline
    /// </summary>
    public async ValueTask<TOutput> ProcessSingleAsync(TInput input, CancellationToken cancellationToken = default)
    {
        return await _processor(input);
    }

    /// <summary>
    /// Processes multiple items in parallel
    /// </summary>
    public async Task<IReadOnlyList<TOutput>> ProcessBatchAsync(
        IEnumerable<TInput> inputs,
        CancellationToken cancellationToken = default)
    {
        var tasks = inputs.Select(input => _processor(input).AsTask()).ToArray();
        var results = await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Completes input and waits for all processing to finish
    /// </summary>
    public async Task CompleteAsync()
    {
        _inputChannel.Writer.Complete();
        await Task.WhenAll(_processingTasks);
        _outputChannel.Writer.Complete();
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var input in _inputChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var output = await _processor(input);
                    await _outputChannel.Writer.WriteAsync(output, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing item of type {InputType}", typeof(TInput).Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing task cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in processing task");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cancellationTokenSource.Cancel();
        _inputChannel.Writer.TryComplete();

        try
        {
            Task.WaitAll(_processingTasks, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for processing tasks to complete");
        }

        _outputChannel.Writer.TryComplete();
        _cancellationTokenSource.Dispose();
        _disposed = true;
    }
}
