namespace GaChatbot.Tests;

using GA.Business.ML.Musical.Enrichment;
using NUnit.Framework;

/// <summary>
/// Tests for ModalCharacteristicIntervalService programmatic interval computation.
/// </summary>
[TestFixture]
public class ModalCharacteristicIntervalServiceTests
{
    [Test]
    public void Instance_LoadsModes_FromDomainModel()
    {
        // Act
        var service = ModalCharacteristicIntervalService.Instance;
        var modeNames = service.GetAllModeNames().ToList();

        // Assert
        Assert.That(modeNames, Is.Not.Empty);
        Assert.That(modeNames.Count, Is.GreaterThanOrEqualTo(7)); // At least 7 major scale modes
        Console.WriteLine($"Loaded {modeNames.Count} modes from domain model.");
    }

    [Test]
    public void GetCharacteristicSemitones_Lydian_ContainsSharp4()
    {
        // Lydian's characteristic interval is #4 (6 semitones)
        var service = ModalCharacteristicIntervalService.Instance;
        
        // Act
        var semitones = service.GetCharacteristicSemitones("Lydian");

        // Assert
        Assert.That(semitones, Is.Not.Null);
        Assert.That(semitones, Does.Contain(6), "Lydian should contain #4 (6 semitones)");
        Console.WriteLine($"Lydian characteristic intervals (semitones): {string.Join(", ", semitones!)}");
    }

    [Test]
    public void GetCharacteristicSemitones_Dorian_ContainsMajor6()
    {
        // Dorian's characteristic interval is Major 6 (9 semitones) with minor 3rd
        var service = ModalCharacteristicIntervalService.Instance;
        
        // Act
        var semitones = service.GetCharacteristicSemitones("Dorian");

        // Assert
        Assert.That(semitones, Is.Not.Null);
        Assert.That(semitones, Does.Contain(9), "Dorian should contain Major 6 (9 semitones)");
        Console.WriteLine($"Dorian characteristic intervals (semitones): {string.Join(", ", semitones!)}");
    }

    [Test]
    public void GetCharacteristicSemitones_Phrygian_ContainsFlat2()
    {
        // Phrygian's characteristic interval is b2 (1 semitone)
        var service = ModalCharacteristicIntervalService.Instance;
        
        // Act
        var semitones = service.GetCharacteristicSemitones("Phrygian");

        // Assert
        Assert.That(semitones, Is.Not.Null);
        Assert.That(semitones, Does.Contain(1), "Phrygian should contain b2 (1 semitone)");
        Console.WriteLine($"Phrygian characteristic intervals (semitones): {string.Join(", ", semitones!)}");
    }

    [Test]
    public void GetCharacteristicSemitones_UnknownMode_ReturnsNull()
    {
        var service = ModalCharacteristicIntervalService.Instance;
        
        // Act
        var semitones = service.GetCharacteristicSemitones("NonExistentMode");

        // Assert
        Assert.That(semitones, Is.Null);
    }

    [Test]
    public void GetAllModeNames_ContainsMajorModes()
    {
        var service = ModalCharacteristicIntervalService.Instance;
        var modeNames = service.GetAllModeNames().ToList();

        // Assert - Major scale modes
        Assert.That(modeNames, Does.Contain("Ionian"));
        Assert.That(modeNames, Does.Contain("Dorian"));
        Assert.That(modeNames, Does.Contain("Phrygian"));
        Assert.That(modeNames, Does.Contain("Lydian"));
        Assert.That(modeNames, Does.Contain("Mixolydian"));
        Assert.That(modeNames, Does.Contain("Aeolian"));
        Assert.That(modeNames, Does.Contain("Locrian"));
    }
}
