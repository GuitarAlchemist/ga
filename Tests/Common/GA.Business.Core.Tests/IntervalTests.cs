namespace GA.Business.Core.Tests;

[TestFixture]
public class IntervalTests
{
    [Test]
    public void Interval_TryParse_dd1()
    {
        // Act
        Interval.Simple.TryParse("bb1", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: bb1, Expected: {Interval.Simple.dd1}, Actual: {interval} (Double-flat 1st = Doubly Diminished Unison)");
        Assert.That(interval, Is.EqualTo(Interval.Simple.dd1), "Parsing 'bb1' should yield a doubly diminished unison (dd1).");
    }

    [Test]
    public void Interval_TryParse_d1()
    {
        // Act
        Interval.Simple.TryParse("b1", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: b1, Expected: {Interval.Simple.d1}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.d1));
    }

    [Test]
    public void Interval_TryParse_P1()
    {
        // Act
        Interval.Simple.TryParse("1", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 1, Expected: {Interval.Simple.P1}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.P1));
    }

    [Test]
    public void Interval_TryParse_A1()
    {
        // Act
        Interval.Simple.TryParse("#1", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: #1, Expected: {Interval.Simple.A1}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.A1));
    }

    [Test]
    public void Interval_TryParse_AA1()
    {
        // Act
        Interval.Simple.TryParse("##1", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: ##1, Expected: {Interval.Simple.AA1}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.AA1));
    }

    [Test]
    public void Interval_TryParse_dd2()
    {
        // Act
        Interval.Simple.TryParse("bbb2", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: bbb2, Expected: {Interval.Simple.dd2}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.dd2));
    }

    [Test]
    public void Interval_TryParse_d2()
    {
        // Act
        Interval.Simple.TryParse("bb2", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: bb2, Expected: {Interval.Simple.d2}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.d2));
    }

    [Test]
    public void Interval_TryParse_m2()
    {
        // Act
        Interval.Simple.TryParse("b2", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: b2, Expected: {Interval.Simple.m2}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.m2));
    }

    [Test]
    public void Interval_TryParse_M2()
    {
        // Act
        Interval.Simple.TryParse("2", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 2, Expected: {Interval.Simple.M2}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.M2));
    }

    [Test]
    public void Interval_TryParse_A2()
    {
        // Act
        Interval.Simple.TryParse("#2", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: #2, Expected: {Interval.Simple.A2}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.A2));
    }

    [Test]
    public void Interval_TryParse_AA2()
    {
        // Act
        Interval.Simple.TryParse("##2", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: ##2, Expected: {Interval.Simple.AA2}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.AA2));
    }

    [Test]
    public void Interval_TryParse_M3()
    {
        // Act
        Interval.Simple.TryParse("3", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 3, Expected: {Interval.Simple.M3}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.M3));
    }

    [Test]
    public void Interval_TryParse_P4()
    {
        // Act
        Interval.Simple.TryParse("4", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 4, Expected: {Interval.Simple.P4}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.P4));
    }

    [Test]
    public void Interval_TryParse_P5()
    {
        // Act
        Interval.Simple.TryParse("5", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 5, Expected: {Interval.Simple.P5}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.P5));
    }

    [Test]
    public void Interval_TryParse_M6()
    {
        // Act
        Interval.Simple.TryParse("6", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 6, Expected: {Interval.Simple.M6}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.M6));
    }

    [Test]
    public void Interval_TryParse_M7()
    {
        // Act
        Interval.Simple.TryParse("7", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 7, Expected: {Interval.Simple.M7}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.M7));
    }

    [Test]
    public void Interval_TryParse_P8()
    {
        // Act
        Interval.Simple.TryParse("8", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 8, Expected: {Interval.Simple.P8}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Simple.P8));
    }

    [Test]
    public void Interval_TryParse_m9()
    {
        // Act
        Interval.Compound.TryParse("b9", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: b9, Expected: {Interval.Compound.m9}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Compound.m9));
    }

    [Test]
    public void Interval_TryParse_M9()
    {
        // Act
        Interval.Compound.TryParse("9", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 9, Expected: {Interval.Compound.M9}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Compound.M9));
    }

    [Test]
    public void Interval_TryParse_m10()
    {
        // Act
        Interval.Compound.TryParse("b10", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: b10, Expected: {Interval.Compound.m10}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Compound.m10));
    }

    [Test]
    public void Interval_TryParse_M10()
    {
        // Act
        Interval.Compound.TryParse("10", null, out var interval);

        // Assert
        TestContext.WriteLine($"Input: 10, Expected: {Interval.Compound.M10}, Actual: {interval}");
        Assert.That(interval, Is.EqualTo(Interval.Compound.M10));
    }
}
