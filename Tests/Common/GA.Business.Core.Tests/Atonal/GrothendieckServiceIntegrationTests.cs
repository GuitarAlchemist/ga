namespace GA.Business.Core.Tests.Atonal;

using System.Collections.Generic;
using System.Linq;
using Atonal.Grothendieck;
using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Grothendieck;
using GA.Business.Core.Notes.Primitives;
using NUnit.Framework;

[TestFixture]
public class GrothendieckServiceIntegrationTests
{
    private IGrothendieckService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new GrothendieckService();
    }

    [Test]
    public void ComputeDelta_AndCost_IsConsistent()
    {
        var source = new PitchClassSet([0, 4, 7]);
        var target = new PitchClassSet([0, 3, 7]);

        var sourceIcv = source.IntervalClassVector;
        var targetIcv = target.IntervalClassVector;

        var delta = _service.ComputeDelta(sourceIcv, targetIcv);
        var cost = _service.ComputeHarmonicCost(delta);

        Assert.That(delta.L1Norm, Is.GreaterThan(0));
        Assert.That(cost, Is.EqualTo(delta.L1Norm * 0.6));
    }

    [Test]
    public void FindNearby_ReturnsSortedResults()
    {
        var source = new PitchClassSet([0, 4, 7]);
        var nearby = _service.FindNearby(source, maxDistance: 3).ToList();

        Assert.That(nearby.Count, Is.Positive);
        Assert.That(nearby[0].Cost, Is.LessThanOrEqualTo(nearby[^1].Cost));
    }

    [Test]
    public void FindShortestPath_FindsPathWithinSteps()
    {
        var source = new PitchClassSet([0, 4, 7]);
        var target = new PitchClassSet([2, 5, 9]);

        var path = _service.FindShortestPath(source, target, maxSteps: 3).ToList();

        Assert.That(path.Count, Is.GreaterThan(1));
        Assert.That(path.First(), Is.EqualTo(source));
        Assert.That(path.Last(), Is.EqualTo(target));
    }
}
