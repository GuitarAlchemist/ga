namespace GA.Core.Tests.Functional;

using Core.Functional;

[TestFixture]
[Category("Functional")]
public class TryTests
{
    [Test]
    public async Task OfAsync_OperationThrows_ReturnsFailure()
    {
        var result = await Try.OfAsync<int>(() => throw new InvalidOperationException("boom"));

        Assert.That(result.IsFailure, Is.True);
    }

    [Test]
    public async Task OfAsync_OperationSucceeds_ReturnsValue()
    {
        var result = await Try.OfAsync(() => Task.FromResult(42));

        Assert.That(result.GetValueOrThrow(), Is.EqualTo(42));
    }

    [Test]
    public void OfAsync_Cancellation_PropagatesInsteadOfBecomingAFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            async () => await Try.OfAsync(() => Task.FromCanceled<int>(cts.Token)),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task OfAsync_DoesNotResumeOnTheCallersSynchronizationContext()
    {
        var context = new CountingSynchronizationContext();
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Try<int>> call;

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            call = Try.OfAsync(() => pending.Task);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        pending.SetResult(7);
        var result = await call;

        Assert.Multiple(() =>
        {
            Assert.That(result.GetValueOrThrow(), Is.EqualTo(7));
            Assert.That(context.PostCount, Is.Zero, "a single-threaded context blocked on this call would deadlock");
        });
    }

    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }
}
