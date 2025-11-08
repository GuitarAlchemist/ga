using System.Threading.Tasks.Dataflow;

namespace GA.Business.Core.Performance;

/// <summary>
/// TPL Dataflow-based pipeline for complex musical data processing
/// </summary>
/// <typeparam name="TInput">Input data type</typeparam>
/// <typeparam name="TOutput">Output data type</typeparam>
[PublicAPI]
public sealed class DataflowPipeline<TInput, TOutput> : IDisposable
{
    private readonly ITargetBlock<TInput> _inputBlock;
    private readonly ISourceBlock<TOutput> _outputBlock;
    private readonly ILogger<DataflowPipeline<TInput, TOutput>> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed;

    public DataflowPipeline(
        Func<TInput, Task<TOutput>> processor,
        ILogger<DataflowPipeline<TInput, TOutput>> logger,
        int maxDegreeOfParallelism = -1,
        int boundedCapacity = 1000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();

        maxDegreeOfParallelism = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

        var executionOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            BoundedCapacity = boundedCapacity,
            CancellationToken = _cancellationTokenSource.Token
        };

        // Create the processing block
        var processingBlock = new TransformBlock<TInput, TOutput>(
            async input =>
            {
                try
                {
                    return await processor(input);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing item of type {InputType}", typeof(TInput).Name);
                    throw;
                }
            },
            executionOptions);

        _inputBlock = processingBlock;
        _outputBlock = processingBlock;

        _logger.LogInformation("DataflowPipeline created with parallelism {MaxDegreeOfParallelism}",
            maxDegreeOfParallelism);
    }

    /// <summary>
    /// Creates a multi-stage pipeline with intermediate processing steps
    /// </summary>
    public static DataflowPipeline<TInput, TFinalOutput> CreateMultiStage<TIntermediate, TFinalOutput>(
        Func<TInput, Task<TIntermediate>> firstStage,
        Func<TIntermediate, Task<TFinalOutput>> secondStage,
        ILogger<DataflowPipeline<TInput, TFinalOutput>> logger,
        int maxDegreeOfParallelism = -1,
        int boundedCapacity = 1000)
    {
        var cancellationTokenSource = new CancellationTokenSource();

        maxDegreeOfParallelism = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

        var executionOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            BoundedCapacity = boundedCapacity,
            CancellationToken = cancellationTokenSource.Token
        };

        // Create processing blocks
        var firstBlock = new TransformBlock<TInput, TIntermediate>(firstStage, executionOptions);
        var secondBlock = new TransformBlock<TIntermediate, TFinalOutput>(secondStage, executionOptions);

        // Link the blocks
        firstBlock.LinkTo(secondBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new DataflowPipeline<TInput, TFinalOutput>(
            firstBlock,
            secondBlock,
            logger,
            cancellationTokenSource);
    }

    private DataflowPipeline(
        ITargetBlock<TInput> inputBlock,
        ISourceBlock<TOutput> outputBlock,
        ILogger<DataflowPipeline<TInput, TOutput>> logger,
        CancellationTokenSource cancellationTokenSource)
    {
        _inputBlock = inputBlock;
        _outputBlock = outputBlock;
        _logger = logger;
        _cancellationTokenSource = cancellationTokenSource;
    }

    /// <summary>
    /// Posts an item to the pipeline for processing
    /// </summary>
    public bool Post(TInput item)
    {
        return _inputBlock.Post(item);
    }

    /// <summary>
    /// Sends an item to the pipeline and waits for acceptance
    /// </summary>
    public async Task<bool> SendAsync(TInput item, CancellationToken cancellationToken = default)
    {
        return await _inputBlock.SendAsync(item, cancellationToken);
    }

    /// <summary>
    /// Receives a processed item from the pipeline
    /// </summary>
    public async Task<TOutput> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        return await _outputBlock.ReceiveAsync(cancellationToken);
    }

    /// <summary>
    /// Processes a batch of items and returns all results
    /// </summary>
    public async Task<IReadOnlyList<TOutput>> ProcessBatchAsync(
        IEnumerable<TInput> inputs,
        CancellationToken cancellationToken = default)
    {
        var inputList = inputs.ToList();
        var results = new List<TOutput>(inputList.Count);

        // Send all inputs
        foreach (var input in inputList)
        {
            await SendAsync(input, cancellationToken);
        }

        // Receive all outputs
        for (int i = 0; i < inputList.Count; i++)
        {
            var output = await ReceiveAsync(cancellationToken);
            results.Add(output);
        }

        return results;
    }

    /// <summary>
    /// Completes the pipeline and waits for all processing to finish
    /// </summary>
    public async Task CompleteAsync()
    {
        _inputBlock.Complete();
        await _outputBlock.Completion;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cancellationTokenSource.Cancel();
        _inputBlock.Complete();

        try
        {
            _outputBlock.Completion.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for pipeline completion");
        }

        _cancellationTokenSource.Dispose();
        _disposed = true;
    }
}
