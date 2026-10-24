namespace GA.Business.Core.Tests.Config;

using Business.Config;
using GA.Domain.Core.Primitives.Notes;

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

    // Instruments.yaml is a mapping of instruments, each a mapping of tunings. The loader used to
    // expect an `Instruments:` list, failed silently and fell back to a hard-coded guitar (1 instrument,
    // 2 tunings). Count the file's own top-level keys and `Tuning:` lines so the test tracks the YAML.
    [Test]
    public void GetAllInstruments_LoadsEveryInstrumentAndTuningInTheYaml()
    {
        var path = ConfigFileLocator.findFile("Instruments.yaml");
        Assert.That(path, Is.Not.Null, "Instruments.yaml not found");
        var lines = File.ReadAllLines(path.Value);
        var expectedInstruments = lines.Count(l => l.Length > 0 && char.IsLetter(l[0]) && l.TrimEnd().EndsWith(':'));
        var expectedTunings = lines.Count(l => l.StartsWith("    Tuning:"));

        var instruments = InstrumentsConfig.getAllInstruments();

        Assert.Multiple(() =>
        {
            Assert.That(expectedInstruments, Is.GreaterThan(100), "sanity: the YAML was found and read");
            Assert.That(instruments.Length, Is.EqualTo(expectedInstruments));
            Assert.That(instruments.Sum(i => i.Tunings.Length), Is.EqualTo(expectedTunings));
            var ukulele = InstrumentsConfig.tryGetInstrument("Ukulele");
            Assert.That(ukulele, Is.Not.Null);
            Assert.That(ukulele.Value.Tunings.Select(t => t.Name), Does.Contain("Baritone"));
        });
    }

    // The harp guitar's `|` separates the neck from the sub-bass strings on purpose; nothing parses that
    // notation yet, and choosing one is a design decision, so those two entries are the known exceptions.
    [Test]
    public void EveryTuningInTheYaml_IsAListOfPitches()
    {
        var unparsable = InstrumentsConfig.getAllInstruments()
            .SelectMany(i => i.Tunings.Select(t => (Instrument: i.Name, t.Name, t.Tuning)))
            .Where(t => t.Instrument != "HarpGuitar")
            .Where(t => !PitchCollection.TryParse(t.Tuning, null, out _))
            .Select(t => $"{t.Instrument}.{t.Name}: {t.Tuning}")
            .ToList();

        Assert.That(unparsable, Is.Empty);
    }

    // "C6" and "D6" are the names of the ukulele's C and D tunings (the chord the open strings sound),
    // not a fifth pitch: a ukulele and a banjolele have four strings.
    [TestCase("SopranoConcertAndTenorC", "G4 C4 E4 A4")]
    [TestCase("SopranoConcertAndTenorD", "A4 D4 F#4 B4")]
    [TestCase("BanjoOrBanjoleteC", "G4 C4 E4 A4")]
    [TestCase("BanjoOrBanjoleteD", "A4 D4 F#4 B4")]
    public void UkuleleTunings_DoNotStartWithTheirName(string tuningName, string expected)
    {
        var tuning = InstrumentsConfig.tryGetInstrument("Ukulele").Value.Tunings.Single(t => t.Name == tuningName);
        Assert.That(tuning.Tuning, Is.EqualTo(expected));
    }

    [Test]
    public void ReloadConfig_DoesNotThrowException() => Assert.DoesNotThrow(() => InstrumentsConfig.reloadConfig());
}
