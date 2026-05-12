namespace GA.Business.ML.Tests;

using GA.Business.ML.Musical.Enrichment;

/// <summary>
/// Tests for <see cref="ModalCharacteristicIntervalService"/> programmatic
/// interval computation. Moved from <c>GaChatbot.Tests</c> on 2026-05-12 —
/// the service lives in <c>GA.Business.ML</c> so its tests belong with
/// the rest of the ML test suite, not in the chatbot host project.
/// </summary>
/// <remarks>
/// Uses the root <c>GA.Business.ML.Tests</c> namespace (matching the
/// sibling <c>DatasetExportTests</c> in the same folder) rather than a
/// nested <c>.Musical</c> namespace — the nested form would shadow the
/// production <c>GA.Business.ML.Musical</c> namespace for any test file
/// that does <c>using GA.Business.ML.Musical;</c>.
/// </remarks>
[TestFixture]
public class ModalCharacteristicIntervalServiceTests
{
    [Test]
    public void Instance_LoadsModes_FromDomainModel()
    {
        var service = ModalCharacteristicIntervalService.Instance;
        var modeNames = service.GetAllModeNames().ToList();

        Assert.That(modeNames, Is.Not.Empty);
        Assert.That(modeNames.Count, Is.GreaterThanOrEqualTo(7),
            "At least 7 major scale modes expected.");
    }

    [Test]
    public void GetCharacteristicSemitones_Lydian_ContainsSharp4()
    {
        var service = ModalCharacteristicIntervalService.Instance;

        var semitones = service.GetCharacteristicSemitones("Lydian");

        Assert.That(semitones, Is.Not.Null);
        Assert.That(semitones, Does.Contain(6),
            "Lydian should contain #4 (6 semitones).");
    }

    [Test]
    public void GetCharacteristicSemitones_Dorian_ContainsMajor6()
    {
        var service = ModalCharacteristicIntervalService.Instance;

        var semitones = service.GetCharacteristicSemitones("Dorian");

        Assert.That(semitones, Is.Not.Null);
        Assert.That(semitones, Does.Contain(9),
            "Dorian should contain Major 6 (9 semitones).");
    }

    [Test]
    public void GetCharacteristicSemitones_Phrygian_ContainsFlat2()
    {
        var service = ModalCharacteristicIntervalService.Instance;

        var semitones = service.GetCharacteristicSemitones("Phrygian");

        Assert.That(semitones, Is.Not.Null);
        Assert.That(semitones, Does.Contain(1),
            "Phrygian should contain b2 (1 semitone).");
    }

    [Test]
    public void GetCharacteristicSemitones_UnknownMode_ReturnsNull()
    {
        var service = ModalCharacteristicIntervalService.Instance;

        var semitones = service.GetCharacteristicSemitones("NonExistentMode");

        Assert.That(semitones, Is.Null);
    }

    [Test]
    public void GetAllModeNames_ContainsMajorModes()
    {
        var service = ModalCharacteristicIntervalService.Instance;
        var modeNames = service.GetAllModeNames().ToList();

        Assert.That(modeNames, Does.Contain("Ionian"));
        Assert.That(modeNames, Does.Contain("Dorian"));
        Assert.That(modeNames, Does.Contain("Phrygian"));
        Assert.That(modeNames, Does.Contain("Lydian"));
        Assert.That(modeNames, Does.Contain("Mixolydian"));
        Assert.That(modeNames, Does.Contain("Aeolian"));
        Assert.That(modeNames, Does.Contain("Locrian"));
    }
}
