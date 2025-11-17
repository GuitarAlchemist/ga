namespace GA.Core.Tests;

using Core;

public class LazyWithExpirationTests
{
    [Test]
    public void ReturnsSameValue_BeforeExpiration()
    {
        // Arrange
        var counter = 0;
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter), TimeSpan.FromMilliseconds(200));

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
            Assert.That(r, Is.EqualTo(1));
        Assert.That(counter, Is.EqualTo(1), "Only one initialization should occur prior to expiration under concurrency");
    }

    [Test]
    public void TimerStartsOnFirstAccess_NotOnConstruction()
    {
        // This test ensures that accessing Value triggers the expiration timer; until then, value is not created.
        var counter = 0;
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter), TimeSpan.FromMilliseconds(80));

        // Wait longer than expiration but without accessing Value; timer should not have started yet.
        Thread.Sleep(120);
        Assert.That(counter, Is.EqualTo(0), "Factory should not be called before first Value access");

        var first = lazy.Value;
        Assert.That(first, Is.EqualTo(1));

        // Now wait for expiration and verify recompute happens on next access
        Thread.Sleep(120);
        var second = lazy.Value;
        Assert.That(second, Is.EqualTo(2));
    }
}
