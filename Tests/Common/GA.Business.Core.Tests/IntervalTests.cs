namespace GA.Business.Core.Tests;

using GA.Business.Core.Intervals;

[TestFixture]
public class IntervalTests
{
    #region IParsable<Simple>

    [Test]
    public void Interval_TryParse_dd1()
    {
        Interval.Simple.TryParse("bb1", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.dd1));
    }

    [Test]
    public void Interval_TryParse_d1()
    {
        Interval.Simple.TryParse("b1", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.d1));
    }

    [Test]
    public void Interval_TryParse_P1()
    {
        Interval.Simple.TryParse("1", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.P1));
    }

    [Test]
    public void Interval_TryParse_A1()
    {
        Interval.Simple.TryParse("#1", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.A1));
    }

    [Test]
    public void Interval_TryParse_AA1()
    {
        Interval.Simple.TryParse("##1", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.AA1));
    }

    [Test]
    public void Interval_TryParse_dd2()
    {
        Interval.Simple.TryParse("bbb2", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.dd2));
    }

    [Test]
    public void Interval_TryParse_d2()
    {
        Interval.Simple.TryParse("bb2", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.d2));
    }

    [Test]
    public void Interval_TryParse_m2()
    {
        Interval.Simple.TryParse("b2", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.m2));
    }

    [Test]
    public void Interval_TryParse_M2()
    {
        Interval.Simple.TryParse("2", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.M2));
    }

    [Test]
    public void Interval_TryParse_A2()
    {
        Interval.Simple.TryParse("#2", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.A2));
    }

    [Test]
    public void Interval_TryParse_AA2()
    {
        Interval.Simple.TryParse("##2", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.AA2));
    }

    [Test]
    public void Interval_TryParse_M3()
    {
        Interval.Simple.TryParse("3", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.M3));
    }

    [Test]
    public void Interval_TryParse_P4()
    {
        Interval.Simple.TryParse("4", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.P4));
    }

    [Test]
    public void Interval_TryParse_P5()
    {
        Interval.Simple.TryParse("5", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.P5));
    }

    [Test]
    public void Interval_TryParse_M6()
    {
        Interval.Simple.TryParse("6", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.M6));
    }

    [Test]
    public void Interval_TryParse_M7()
    {
        Interval.Simple.TryParse("7", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.M7));
    }

    [Test]
    public void Interval_TryParse_P8()
    {
        Interval.Simple.TryParse("8", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Simple.P8));
    }

    [Test]
    public void Interval_TryParse_m9()
    {
        Interval.Compound.TryParse("b9", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Compound.m9));
    }

    [Test]
    public void Interval_TryParse_M9()
    {
        Interval.Compound.TryParse("9", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Compound.M9));
    }

    [Test]
    public void Interval_TryParse_m10()
    {
        Interval.Compound.TryParse("b10", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Compound.m10));
    }

    [Test]
    public void Interval_TryParse_M10()
    {
        Interval.Compound.TryParse("10", null, out var interval);

        Assert.That(interval, Is.EqualTo(Interval.Compound.M10));
    }

    #endregion
}