namespace GA.Core.Tests;

using Utilities;

public class LazyWithExpirationTests
{
    [Test]
    public void ReturnsSameValue_BeforeExpiration()
    {
        // Arrange
        var counter = 0;
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter),
            TimeSpan.FromMilliseconds(200));

        // Act
        var v1 = lazy.Value;
        var v2 = lazy.Value;

        // Assert
        Assert.That(v1, Is.EqualTo(1));
        Assert.That(v2, Is.EqualTo(1));
        Assert.That(counter, Is.EqualTo(1), "Factory should be called only once before expiration");
    }

    [Test]
    public void Recomputes_AfterExpiration_OnNextAccess()
    {
        // Arrange
        var counter = 0;
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter), TimeSpan.FromMilliseconds(60));

        // Act
        var first = lazy.Value; // starts expiration timer on first access
        Assert.That(first, Is.EqualTo(1));

        // Wait for expiration to elapse with a small buffer
        Thread.Sleep(120);

        var second = lazy.Value; // should recompute now

        // Assert
        Assert.That(second, Is.EqualTo(2));
        Assert.That(counter, Is.EqualTo(2));
    }

    [Test]
    public void Reset_Forces_Recompute_Immediately()
    {
        // Arrange
        var counter = 0;
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter), TimeSpan.FromSeconds(5));

        // Act
        var first = lazy.Value;
        lazy.Reset();
        var second = lazy.Value;

        // Assert
        Assert.That(first, Is.EqualTo(1));
        Assert.That(second, Is.EqualTo(2));
        Assert.That(counter, Is.EqualTo(2));
    }

    [Test]
    public void ConcurrentAccess_InitializesOnlyOnce_BeforeExpiration()
    {
        // Arrange
        var counter = 0;
        var lazy = new LazyWithExpiration<int>(() =>
        {
            // Simulate work
            Thread.Sleep(20);
            return Interlocked.Increment(ref counter);
        }, TimeSpan.FromMilliseconds(500));

        // Act
        var results = new int[16];
        Parallel.For(0, results.Length, i => { results[i] = lazy.Value; });

        // Assert
        foreach (var r in results)
        {
            Assert.That(r, Is.EqualTo(1));
        }

        Assert.That(counter, Is.EqualTo(1),
            "Only one initialization should occur prior to expiration under concurrency");
    }

    [Test]
    public void TimerStartsOnFirstAccess_NotOnConstruction()
    {
        // This test ensures that accessing Value triggers the expiration timer; until then, value is not created.
        var counter = 0;
        var expiration = TimeSpan.FromMilliseconds(200);
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter), expiration);

        // Wait longer than expiration but without accessing Value; timer should not have started yet.
        Thread.Sleep(400);
        Assert.That(counter, Is.EqualTo(0), "Factory should not be called before first Value access");

        var first = lazy.Value;
        Assert.That(first, Is.EqualTo(1));

        // Spin until expiration fires (or we hit a hard cap). Threadpool-scheduled Timer
        // callbacks under CI load can lag past a fixed Thread.Sleep — observed 525ms on
        // a slow runner where expiration=100ms + Sleep(300) still saw counter=1. The
        // window is intentionally generous (10x expiration) so this only flips when the
        // timer is genuinely broken, not when CI is busy.
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(expiration.TotalMilliseconds * 10);
        int second;
        do
        {
            Thread.Sleep(50);
            second = lazy.Value;
        }
        while (second == 1 && DateTime.UtcNow < deadline);

        Assert.That(second, Is.EqualTo(2));
    }
}
