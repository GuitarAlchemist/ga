namespace GA.Business.ML.Tests;

using System.Threading.Tasks;
using GA.Business.ML.Tabs;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Musical.Analysis;
using GA.Business.ML.Tests.TestInfrastructure;
using NUnit.Framework;

[TestFixture]
public class TabCadenceIntegrationTests
{
    private TabAnalysisService _service;

    [SetUp]
    public void Setup()
    {
        _service = TestServices.CreateTabAnalysisService();
    }

    [Test]
    public async Task Detect_PerfectAuthenticCadence_FromTab()
    {
        // G -> C (V-I) in C Major
        // G: 3-5-5-4-3-3 (G, D, G, B, D, G)
        // C: x-3-2-0-1-0 (C, E, G, C, E)
        var tab = @"
e|---3---0---|
B|---3---1---|
G|---4---0---|
D|---5---2---|
A|---5---3---|
E|---3-------|
";
        var result = await _service.AnalyzeAsync(tab);

        Assert.That(result.Events.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(result.DetectedCadence, Is.Not.Null);
        Assert.That(result.DetectedCadence, Does.Contain("Perfect Authentic Cadence"));
        Assert.That(result.DetectedCadence, Does.Contain("Key of C"));
    }

    [Test]
    public async Task Detect_AndalusianCadence_FromTab()
    {
        // Am -> G -> F -> E (i-VII-VI-V)
        var tab = @"
e|---0---3---1---0---|
B|---1---0---1---0---|
G|---2---0---2---1---|
D|---2---0---3---2---|
A|---0---2---3---2---|
E|-------3---1---0---|
";
        var result = await _service.AnalyzeAsync(tab);

        Assert.That(result.DetectedCadence, Is.Not.Null);
        Assert.That(result.DetectedCadence, Does.Contain("Andalusian Cadence"));
    }
}
