namespace GA.Business.Core.Tests.Atonal;

using System.Linq;
using NUnit.Framework;
using Domain.Core.Theory.Atonal;

[TestFixture]
public class SetClassToStringTests
{
    // SetClass.ToString() must uniquely identify a set class. Cardinality + interval-class
    // vector are NOT unique: the 23 Z-related pairs in 12-TET (e.g. all-interval tetrachords
    // {0,1,4,6} and {0,1,3,7}) share both. Equals/GetHashCode key on PrimeForm, so identity
    // was always correct — but the label conflated 46 set classes into 23 strings until the
    // prime form was added. Surfaced by the DuckDB domain-invariants sweep.
    [Test]
    public void ToString_UniquelyIdentifies_EverySetClass()
    {
        var collisions = SetClass.Items
            .GroupBy(sc => sc.ToString())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.That(collisions, Is.Empty,
            "SetClass.ToString() must be unique. Colliding labels (Z-related pairs need the " +
            $"prime form in the label): {string.Join("; ", collisions)}");
    }

    // The label-uniqueness must equal object-identity uniqueness: as many distinct labels as
    // distinct set classes. Pins that ToString tracks Equals.
    [Test]
    public void DistinctLabels_EqualsDistinctSetClasses()
    {
        var distinctLabels = SetClass.Items.Select(sc => sc.ToString()).Distinct().Count();
        var distinctClasses = SetClass.Items.Distinct().Count();

        Assert.That(distinctLabels, Is.EqualTo(distinctClasses));
    }
}
