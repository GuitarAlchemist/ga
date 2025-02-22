namespace GA.Business.Core.Tests.Config;

using Business.Config;

[TestFixture]
public class InstrumentsConfigTests
{
    [Test]
    public void GetAllInstruments_ReturnsNonEmptyList()
    {
        var instruments = InstrumentsConfig.getAllInstruments();
        Assert.That(instruments, Is.Not.Empty);
    }

    [Test]
    public void ListAllInstrumentNames_ReturnsNonEmptyList()
    {
        var instrumentNames = InstrumentsConfig.listAllInstrumentNames();
        Assert.That(instrumentNames, Is.Not.Empty);
    }

    [Test]
    public void ListAllInstrumentTunings_ReturnsNonEmptyList()
    {
        var instrumentTunings = InstrumentsConfig.listAllInstrumentTunings();
        Assert.That(instrumentTunings, Is.Not.Empty);
    }

    [Test]
    public void FindInstrumentsByName_ReturnsMatchingInstruments()
    {
        var searchTerm = "Guitar";
        var matchingInstruments = InstrumentsConfig.findInstrumentsByName(searchTerm);
        Assert.That(matchingInstruments, Is.Not.Empty);
        Assert.That(matchingInstruments.All(i => i.Name.ToLower().Contains(searchTerm.ToLower())));
    }

    [Test]
    public void TryGetInstrument_ReturnsCorrectInstrument()
    {
        var instrumentName = "Guitar";
        var instrument = InstrumentsConfig.tryGetInstrument(instrumentName);
        Assert.That(instrument, Is.Not.Null);
        Assert.That(instrument.Value.Name, Is.EqualTo(instrumentName));
    }

    [Test]
    public void TryGetInstrument_ReturnsNoneForNonexistentInstrument()
    {
        var instrumentName = "NonexistentInstrument";
        var instrument = InstrumentsConfig.tryGetInstrument(instrumentName);
        Assert.That(instrument, Is.Null);
    }

    [Test]
    public void ReloadConfig_DoesNotThrowException()
    {
        Assert.DoesNotThrow(() => InstrumentsConfig.reloadConfig());
    }
}