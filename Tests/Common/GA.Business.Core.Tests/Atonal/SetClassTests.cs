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
        // Arrange
        var items = SetClass.Items;

        // Act
        var count = items.Count;

        // Assert
        Assert.That(count, Is.GreaterThan(0));
        // The exact count may change as the implementation evolves, so we just verify it's non-empty
    }

    [Test(TestOf = typeof(SetClass))]
    public void ModalItems_ReturnsSubsetOfItems()
    {
        // Arrange
        var allItems = SetClass.Items;
        var modalItems = SetClass.ModalItems;

        // Act
        var modalCount = modalItems.Count;

        // Assert
        Assert.That(modalCount, Is.GreaterThan(0));
        Assert.That(modalCount, Is.LessThanOrEqualTo(allItems.Count));
        Assert.That(modalItems.All(item => item.IsModal), Is.True);
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
        Assert.That(setClass, Is.Not.Null);
        Assert.That(setClass.Cardinality.Value, Is.EqualTo(3)); // Major triad has 3 notes
        Assert.That(setClass.PrimeForm, Is.Not.Null);
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

        // Assert
        Assert.That(setClass1, Is.EqualTo(setClass2)); // Both are major triads, so same set class
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

        // Assert
        Assert.That(setClass1, Is.Not.EqualTo(setClass2)); // Major and diminished triads are different set classes
    }
}
