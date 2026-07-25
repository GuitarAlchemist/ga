namespace GA.Core.Tests;

using Utilities;

public class LazyWithExpirationTests
{
    [Test]
    public void ReturnsSameValue_BeforeExpiration()
    {
        // Arrange
        var counter = 0;
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter),
            TimeSpan.FromMilliseconds(200), () => now);

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
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter),
            TimeSpan.FromMilliseconds(60), () => now);

        // Act
        var first = lazy.Value; // starts expiration window on first access
        Assert.That(first, Is.EqualTo(1));

        // Advance the fake clock past expiration; no real waiting involved.
        now = now.AddMilliseconds(120);

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
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter),
            TimeSpan.FromSeconds(5), () => now);

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
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter),
            TimeSpan.FromMilliseconds(500), () => now);

        // Act
        var results = new int[16];
        Assert.DoesNotThrow(() => Parallel.For(0, results.Length, i => { results[i] = lazy.Value; }));

        // Assert
        foreach (var r in results)
        {
            Assert.That(r, Is.EqualTo(1));
        }

        Assert.That(counter, Is.EqualTo(1),
            "Only one initialization should occur prior to expiration under concurrency");
    }

    [Test]
    public void ValueNotComputed_UntilFirstAccess()
    {
        // Arrange
        var counter = 0;
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter),
            TimeSpan.FromMilliseconds(200), () => now);

        // Advancing the clock without accessing Value should not trigger computation.
        now = now.AddMilliseconds(400);
        Assert.That(counter, Is.EqualTo(0), "Factory should not be called before first Value access");

        // Act
        var first = lazy.Value;

        // Assert
        Assert.That(first, Is.EqualTo(1));
    }

    [Test]
    public void DefaultConstructor_UsesRealClock_AndStillWorks()
    {
        // Arrange
        var counter = 0;
        var lazy = new LazyWithExpiration<int>(() => Interlocked.Increment(ref counter), TimeSpan.FromMinutes(5));

        // Act
        var v1 = lazy.Value;
        var v2 = lazy.Value;

        // Assert
        Assert.That(v1, Is.EqualTo(1));
        Assert.That(v2, Is.EqualTo(1));
    }
}
