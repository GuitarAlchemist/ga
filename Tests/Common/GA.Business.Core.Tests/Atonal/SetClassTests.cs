namespace GA.Business.Core.Tests.Atonal;

using Core.Atonal;
using Core.Notes;
using Extensions;

[TestFixture]
public class SetClassTests
{
    [Test(TestOf = typeof(SetClass))]
    public void Items_ReturnsNonEmptyCollection()
    {
        // Act
        var items = SetClass.Items;
        var count = items.Count;

        // Assert
        TestContext.WriteLine($"Total SetClass items: {count}");
        Assert.That(count, Is.GreaterThan(0));
        // The exact count may change as the implementation evolves, so we just verify it's non-empty
    }

    [Test(TestOf = typeof(SetClass))]
    public void ModalItems_ReturnsSubsetOfItems()
    {
        // Act
        var allItems = SetClass.Items;
        var modalItems = SetClass.ModalItems;
        var modalCount = modalItems.Count;

        // Assert
        TestContext.WriteLine($"Total SetClasses: {allItems.Count}, Modal SetClasses: {modalCount}");
        Assert.Multiple(() =>
        {
            Assert.That(modalCount, Is.GreaterThan(0));
            Assert.That(modalCount, Is.LessThanOrEqualTo(allItems.Count));
            Assert.That(modalItems.All(item => item.IsModal), Is.True);
        });
    }

    [Test(TestOf = typeof(SetClass))]
    public void Constructor_WithPitchClassSet_CreatesSetClass()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();

        // Act
        var setClass = new SetClass(majorTriadPcs);

        // Assert
        TestContext.WriteLine($"Input: {sMajorTriadInput}, SetClass Cardinality: {setClass.Cardinality.Value}, Prime Form: {setClass.PrimeForm}");
        Assert.Multiple(() =>
        {
            Assert.That(setClass, Is.Not.Null);
            Assert.That(setClass.Cardinality.Value, Is.EqualTo(3)); // Major triad has 3 notes
            Assert.That(setClass.PrimeForm, Is.Not.Null);
        });
    }

    [Test(TestOf = typeof(SetClass))]
    public void Equals_WithSameSetClass_ReturnsTrue()
    {
        // Arrange
        const string sCMajorTriadInput = "C E G";
        const string sGMajorTriadInput = "G B D";
        var cMajorTriadNotes = AccidentedNoteCollection.Parse(sCMajorTriadInput);
        var gMajorTriadNotes = AccidentedNoteCollection.Parse(sGMajorTriadInput);
        var cMajorTriadPcs = cMajorTriadNotes.ToPitchClassSet();
        var gMajorTriadPcs = gMajorTriadNotes.ToPitchClassSet();

        // Act
        var setClass1 = new SetClass(cMajorTriadPcs);
        var setClass2 = new SetClass(gMajorTriadPcs);
        var areEqual = setClass1.Equals(setClass2);

        // Assert
        TestContext.WriteLine($"SetClass1 (C Maj): {setClass1.PrimeForm}, SetClass2 (G Maj): {setClass2.PrimeForm}, Equal: {areEqual}");
        Assert.That(areEqual, Is.True); // Both are major triads, so same set class
    }

    [Test(TestOf = typeof(SetClass))]
    public void Equals_WithDifferentSetClass_ReturnsFalse()
    {
        // Arrange
        const string sMajorTriadInput = "C E G";
        const string sDiminishedTriadInput = "C Eb Gb";
        var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
        var dimTriadNotes = AccidentedNoteCollection.Parse(sDiminishedTriadInput);
        var majorTriadPcs = majorTriadNotes.ToPitchClassSet();
        var dimTriadPcs = dimTriadNotes.ToPitchClassSet();

        // Act
        var setClass1 = new SetClass(majorTriadPcs);
        var setClass2 = new SetClass(dimTriadPcs);
        var areEqual = setClass1.Equals(setClass2);

        // Assert
        TestContext.WriteLine($"SetClass1 (Maj): {setClass1.PrimeForm}, SetClass2 (Dim): {setClass2.PrimeForm}, Equal: {areEqual}");
        Assert.That(areEqual, Is.False); // Major and diminished triads are different set classes
    }

    [Test(TestOf = typeof(SetClass))]
    public void GetMagnitudeSpectrum_IsInvariantUnderTransposition()
    {
        // Arrange
        var cMajorTriad = new SetClass(AccidentedNoteCollection.Parse("C E G").ToPitchClassSet());
        var dMajorTriad = new SetClass(AccidentedNoteCollection.Parse("D F# A").ToPitchClassSet());

        // Act
        var cSpectrum = cMajorTriad.GetMagnitudeSpectrum();
        var dSpectrum = dMajorTriad.GetMagnitudeSpectrum();

        // Assert
        TestContext.WriteLine($"C Major Spectrum energy: {cSpectrum.Sum():F4}, D Major Spectrum energy: {dSpectrum.Sum():F4}");
        Assert.That(cSpectrum.Length, Is.EqualTo(dSpectrum.Length));
        Assert.Multiple(() =>
        {
            for (var i = 0; i < cSpectrum.Length; i++)
            {
                Assert.That(cSpectrum[i], Is.EqualTo(dSpectrum[i]).Within(1e-9),
                    $"Magnitude mismatch at bin {i}");
            }
        });
    }

    [Test(TestOf = typeof(SetClass))]
    public void GetSpectralCentroid_ReturnsExpectedValueForSingletonSet()
    {
        // Arrange
        var singleton = new SetClass(AccidentedNoteCollection.Parse("C").ToPitchClassSet());

        // Act
        var centroid = singleton.GetSpectralCentroid();

        // Assert
        TestContext.WriteLine($"Singleton Set (C) Spectral Centroid: {centroid:F4}");
        Assert.That(centroid, Is.EqualTo(5.5).Within(1e-6));
    }
}
