namespace GA.Business.Core.Tests.Atonal;

using Core.Atonal;
using Core.Atonal.Primitives;
using Core.Notes;
using Extensions;

public class PitchClassSetIdTests
{
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSetId_Complement()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();

        // Act
        var id = majorTriadPcs.Id; // 145 value / 000010010001 binary value
        var complementId = id.Complement;

        // Assert
        TestContext.WriteLine($"Input: {sMajorTriadInput}, ID: {id}, Complement ID: {complementId}, Complement Binary: {complementId.BinaryValue}");
        Assert.Multiple(() =>
        {
            Assert.That(complementId.BinaryValue, Is.EqualTo("111101101110"));
            Assert.That(complementId.Value, Is.EqualTo(3950));
        });
    }

    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSetId_EqualityComparer_Complement()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();
        var id1 = majorTriadPcs.Id;
        var id2 = id1.Complement;

        // Act
        var id1Hashcode = PitchClassSetId.ComplementComparer.GetHashCode(id1);
        var id2Hashcode = PitchClassSetId.ComplementComparer.GetHashCode(id2);

        // Assert
        TestContext.WriteLine($"ID1: {id1}, ID2 (Complement): {id2}");
        TestContext.WriteLine($"Hashcode 1: {id1Hashcode}, Hashcode 2: {id2Hashcode}");
        Assert.That(id1Hashcode, Is.EqualTo(id2Hashcode));
    }

    [Test(TestOf = typeof(PitchClassSetId))]
    public void Test_PitchClassSetId_Equivalences()
    {
        // Arrange
        var equivalences = PitchClassSetIdEquivalences.Instance;
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();
        var id = majorTriadPcs.Id;

        // Act
        var features = new PitchClassSetIdEquivalences.SetClassFeatures(id);

        // Assert
        TestContext.WriteLine($"ID: {id}, Features ID: {features.Id}");
        TestContext.WriteLine($"Complements: {string.Join(", ", features.Complements)}");

        Assert.Multiple(() =>
        {
            Assert.That(features, Is.Not.Null);
            Assert.That(features.Id, Is.EqualTo(id));
            Assert.That(features.Complements, Contains.Item(id));
            Assert.That(features.Complements, Contains.Item(id.Complement));
            Assert.That(features.Inversions, Contains.Item(id));
            Assert.That(features.Rotations, Is.Not.Empty);
        });
    }

    [Test(TestOf = typeof(PitchClassSetId))]
    public void Test_PitchClassSetId_CountNormalFormIds()
    {
        // Act
        var allIds = PitchClassSetId.Items.ToList();
        var normalFormIds = allIds.Where(id => id.ToPitchClassSet().IsNormalForm).ToImmutableList();

        // Calculate statistics
        var totalCount = allIds.Count;
        var normalFormCount = normalFormIds.Count;
        var percentage = (double)normalFormCount / totalCount * 100;

        // Assert
        TestContext.WriteLine($"Total PitchClassSetId count: {totalCount}");
        TestContext.WriteLine($"Normal form count: {normalFormCount}");
        TestContext.WriteLine($"Percentage in normal form: {percentage:F2}%");

        // Group by cardinality
        var groupedByCardinality = normalFormIds
            .GroupBy(id => id.ToPitchClassSet().Count)
            .OrderBy(g => g.Key)
            .Select(g => new { Cardinality = g.Key, Count = g.Count() })
            .ToList();

        TestContext.WriteLine("\nNormal form counts by cardinality:");
        foreach (var group in groupedByCardinality)
        {
            TestContext.WriteLine($"Cardinality {group.Cardinality}: {group.Count}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(normalFormCount, Is.LessThan(totalCount));
            Assert.That(normalFormCount, Is.GreaterThan(0));
            Assert.That(percentage, Is.GreaterThan(0).And.LessThan(100));
        });
    }
}
