namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class KeyTests
{
    [Test]
    public void GetItems_Major_ReturnsMajorKeys()
    {
        // Act
        var majorKeys = Key.GetItems(KeyMode.Major);
        var keyNames = majorKeys.Select(key => key.ToString()).ToList();

        // Assert
        TestContext.WriteLine($"Major Keys: ExpectedCount=15, ActualCount={majorKeys.Count} (Circle of Fifths: Cb through C#)");
        TestContext.WriteLine($"Sample Major Keys: {string.Join(", ", keyNames)}");

        Assert.Multiple(() =>
        {
            Assert.That(majorKeys.Count, Is.EqualTo(15), "There should be exactly 15 major keys (7 sharps, 7 flats, and C natural).");
            Assert.That(majorKeys.All(key => key.KeyMode == KeyMode.Major), "All returned keys must be in Major mode.");
            Assert.That(keyNames, Is.EquivalentTo(new string[]
            {
                "Key of Cb", "Key of Gb", "Key of Db", "Key of Ab", "Key of Eb", "Key of Bb", "Key of F",
                "Key of C", "Key of G", "Key of D", "Key of A", "Key of E", "Key of B", "Key of F#", "Key of C#"
            }), "Major key names should match standard circle of fifths nomenclature.");
        });
    }

    [Test]
    public void GetItems_Minor_ReturnsMinorKeys()
    {
        // Act
        var minorKeys = Key.GetItems(KeyMode.Minor);
        var keyNames = minorKeys.Select(key => key.ToString()).ToList();

        // Assert
        TestContext.WriteLine($"Minor Keys ({minorKeys.Count}): {string.Join(", ", keyNames)}");

        Assert.Multiple(() =>
        {
            Assert.That(minorKeys.Count, Is.EqualTo(15));
            Assert.That(minorKeys.All(key => key.KeyMode == KeyMode.Minor));
            Assert.That(keyNames, Is.EquivalentTo(new string[]
            {
                "Key of Abm",
                "Key of Ebm",
                "Key of Bbm",
                "Key of Fm",
                "Key of Cm",
                "Key of Gm",
                "Key of Dm",
                "Key of Am",
                "Key of Em",
                "Key of Bm",
                "Key of F#m",
                "Key of C#m",
                "Key of G#m",
                "Key of D#m",
                "Key of A#m"
            }));
        });
    }

    [Test]
    public void Items_ReturnsAllKeys()
    {
        // Act
        var allKeys = Key.Items;
        var keyNames = allKeys.Select(key => key.ToString()).ToList();

        // Assert
        TestContext.WriteLine($"All Keys ({allKeys.Count}): {string.Join(", ", keyNames)}");

        Assert.Multiple(() =>
        {
            Assert.That(allKeys.Count, Is.EqualTo(30));
            Assert.That(allKeys.Count(key => key.KeyMode == KeyMode.Major), Is.EqualTo(15));
            Assert.That(allKeys.Count(key => key.KeyMode == KeyMode.Minor), Is.EqualTo(15));

            Assert.That(keyNames, Is.EquivalentTo(new string[]
            {
                "Key of Cb",
                "Key of Gb",
                "Key of Db",
                "Key of Ab",
                "Key of Eb",
                "Key of Bb",
                "Key of F",
                "Key of C",
                "Key of G",
                "Key of D",
                "Key of A",
                "Key of E",
                "Key of B",
                "Key of F#",
                "Key of C#",
                "Key of Abm",
                "Key of Ebm",
                "Key of Bbm",
                "Key of Fm",
                "Key of Cm",
                "Key of Gm",
                "Key of Dm",
                "Key of Am",
                "Key of Em",
                "Key of Bm",
                "Key of F#m",
                "Key of C#m",
                "Key of G#m",
                "Key of D#m",
                "Key of A#m"
            }));
        });
    }
}
