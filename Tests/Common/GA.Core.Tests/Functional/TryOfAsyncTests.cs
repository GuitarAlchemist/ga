namespace GA.Core.Tests.Functional;

using Core.Functional;

[TestFixture]
[Category("Functional")]
public class TryOfAsyncTests
{
    [Test]
    public async Task OfAsync_Failure_IsCaptured()
    {
        var result = await Try.OfAsync<int>(() => Task.FromException<int>(new InvalidOperationException("boom")));

        Assert.That(result.IsFailure, Is.True);
    }

    [Test]
    public void OfAsync_Cancellation_PropagatesInsteadOfBecomingAFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(() =>
            Try.OfAsync(() => Task.Delay(1000, cts.Token).ContinueWith(_ => 1, cts.Token)));
    }

    [Test]
    public void OfAsync_BlockedOnUnderSingleThreadedContext_DoesNotDeadlock()
    {
        var previous = SynchronizationContext.Current;
        var context = new SingleThreadContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var task = Try.OfAsync(async () =>
            {
                await Task.Delay(10).ConfigureAwait(false);
                return 42;
            });

            // Nothing pumps the context: an await that resumes on it would never complete.
            Assert.That(task.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(task.Result.IsSuccess, Is.True);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>A context that queues continuations and never runs them.</summary>
    private sealed class SingleThreadContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }
}
