using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace GA.Business.Core.Performance;

/// <summary>
/// Reactive Extensions (Rx) based processor for real-time musical data streams
/// </summary>
/// <typeparam name="TInput">Input data type</typeparam>
/// <typeparam name="TOutput">Output data type</typeparam>
[PublicAPI]
public sealed class ReactiveProcessor<TInput, TOutput> : IDisposable
{
    private readonly Subject<TInput> _inputSubject;
    private readonly IObservable<TOutput> _outputObservable;
    private readonly ILogger<ReactiveProcessor<TInput, TOutput>> _logger;
    private readonly CompositeDisposable _disposables;
    private bool _disposed;

    public ReactiveProcessor(
        Func<TInput, Task<TOutput>> processor,
        ILogger<ReactiveProcessor<TInput, TOutput>> logger,
        IScheduler? scheduler = null,
        int maxConcurrency = -1,
        TimeSpan? throttleInterval = null,
        int bufferSize = 100)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inputSubject = new Subject<TInput>();
        _disposables = new CompositeDisposable();

        scheduler ??= TaskPoolScheduler.Default;
        maxConcurrency = maxConcurrency == -1 ? Environment.ProcessorCount : maxConcurrency;

        var pipeline = _inputSubject.AsObservable();

        // Add throttling if specified
        if (throttleInterval.HasValue)
        {
            pipeline = pipeline.Throttle(throttleInterval.Value, scheduler);
        }

        // Add buffering for batch processing
        if (bufferSize > 1)
        {
            pipeline = pipeline.Buffer(bufferSize).SelectMany(batch => batch);
        }

        // Process with controlled concurrency
        _outputObservable = pipeline
            .Select(input => Observable.FromAsync(() => ProcessWithLogging(input, processor)))
            .Merge(maxConcurrency)
            .ObserveOn(scheduler)
            .Publish()
            .RefCount();

        _disposables.Add(_inputSubject);

        _logger.LogInformation("ReactiveProcessor created with concurrency {MaxConcurrency}", maxConcurrency);
    }

    /// <summary>
    /// Gets the observable stream of processed outputs
    /// </summary>
    public IObservable<TOutput> Output => _outputObservable;

    /// <summary>
    /// Pushes an input item into the processing stream
    /// </summary>
    public void Push(TInput input)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ReactiveProcessor<TInput, TOutput>));
        _inputSubject.OnNext(input);
    }

    /// <summary>
    /// Creates a windowed processor that processes items in time-based windows
    /// </summary>
    public static ReactiveProcessor<TInput, IList<TOutput>> CreateWindowed(
        Func<IList<TInput>, Task<IList<TOutput>>> batchProcessor,
        ILogger<ReactiveProcessor<TInput, IList<TOutput>>> logger,
        TimeSpan windowSize,
        IScheduler? scheduler = null)
    {
        scheduler ??= TaskPoolScheduler.Default;

        var inputSubject = new Subject<TInput>();
        var disposables = new CompositeDisposable { inputSubject };

        var outputObservable = inputSubject
            .Window(windowSize, scheduler)
            .SelectMany(window => window.ToList())
            .Where(batch => batch.Count > 0)
            .Select(batch => Observable.FromAsync(() => batchProcessor(batch)))
            .Merge()
            .ObserveOn(scheduler)
            .Publish()
            .RefCount();

        return new ReactiveProcessor<TInput, IList<TOutput>>(
            inputSubject, outputObservable, logger, disposables);
    }

    /// <summary>
    /// Creates a filtered processor that only processes items matching a predicate
    /// </summary>
    public ReactiveProcessor<TInput, TOutput> Where(Func<TInput, bool> predicate)
    {
        var filteredInput = _inputSubject.Where(predicate);
        var filteredOutput = _outputObservable; // Output is already filtered by input

        return new ReactiveProcessor<TInput, TOutput>(
            filteredInput, filteredOutput, _logger, _disposables);
    }

    private ReactiveProcessor(
        IObservable<TInput> input,
        IObservable<TOutput> output,
        ILogger<ReactiveProcessor<TInput, TOutput>> logger,
        CompositeDisposable disposables)
    {
        _inputSubject = new Subject<TInput>();
        _outputObservable = output;
        _logger = logger;
        _disposables = disposables;

        // Connect the filtered input to our subject
        _disposables.Add(input.Subscribe(_inputSubject));
    }

    private ReactiveProcessor(
        Subject<TInput> inputSubject,
        IObservable<TOutput> outputObservable,
        ILogger<ReactiveProcessor<TInput, TOutput>> logger,
        CompositeDisposable disposables)
    {
        _inputSubject = inputSubject;
        _outputObservable = outputObservable;
        _logger = logger;
        _disposables = disposables;
    }

    private async Task<TOutput> ProcessWithLogging(TInput input, Func<TInput, Task<TOutput>> processor)
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
    }

    /// <summary>
    /// Completes the input stream
    /// </summary>
    public void Complete()
    {
        _inputSubject.OnCompleted();
    }

    /// <summary>
    /// Signals an error in the input stream
    /// </summary>
    public void Error(Exception error)
    {
        _inputSubject.OnError(error);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _inputSubject.OnCompleted();
        _disposables.Dispose();
        _disposed = true;
    }
}
