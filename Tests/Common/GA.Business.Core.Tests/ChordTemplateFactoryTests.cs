namespace GA.Business.Core.Tests;

using GA.Business.Core.Atonal;
using Scales;

[TestFixture]
public class ChordTemplateFactoryTests
{
    [Test]
    public void CreateAllChordTemplates_ReturnsNonEmptyList()
    {
        // Act
        var chordTemplates = ChordTemplateFactory.CreateAllChordTemplates();

        // Assert
        Assert.That(chordTemplates, Is.Not.Empty);
    }

    [Test]
    public void CreateAllChordTemplates_ContainsMajorTriad()
    {
        // Arrange
        var majorTriadPcs = PitchClassSet.FromId(145); // C E G

        // Act
        var chordTemplates = ChordTemplateFactory.CreateAllChordTemplates();

        // Assert
        Assert.That(chordTemplates, Has.Some.Matches<ChordTemplate>(ct => ct.PitchClassSet.Equals(majorTriadPcs)));
    }

    [Test]
    public void CreateAllChordTemplates_MajorTriadHasAssociatedScales()
    {
        // Arrange
        var majorTriadPcs = PitchClassSet.FromId(145); // C E G

        // Act
        var chordTemplates = ChordTemplateFactory.CreateAllChordTemplates();
        var majorTriadTemplate = chordTemplates.FirstOrDefault(ct => ct.PitchClassSet.Equals(majorTriadPcs));

        // Assert
    }
}