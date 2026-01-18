namespace GA.Business.ML.Tests;

using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Tabs;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using NUnit.Framework;

[TestFixture]
public class AdditionalScenariosTests
{
    private MusicalEmbeddingGenerator _generator = null!;
    private TabAnalysisService _tabAnalysisService = null!;

    [SetUp]
    public void Setup()
    {
        _generator = TestInfrastructure.TestServices.CreateGenerator();
        _tabAnalysisService = new TabAnalysisService(new TabTokenizer(), new TabToPitchConverter(), _generator);
    }

    [Test]
    public async Task Test_EndToEnd_TabToTags()
    {
        // Tab: C Major Scale run
        var tab = @"
e|-----------------|
B|-----------------|
G|-----------------|
D|----------2--4---|
A|----2--3---------|
E|-3---------------|
"; 
        var result = await _tabAnalysisService.AnalyzeAsync(tab);
        
        Assert.That(result.Events.Count, Is.EqualTo(5));
        
        var lastEvent = result.Events.Last();
        // D string (50) + 4 = 54 (F#3). PC = 6.
        Assert.That(lastEvent.Document.PitchClasses, Contains.Item(6));
        
        // Verify Forte Code is populated on the document
        Assert.That(lastEvent.Document.ForteCode, Is.Not.Null.And.Not.Empty);
        Assert.That(lastEvent.Document.ForteCode, Is.EqualTo("1-1")); // Monad
    }
}