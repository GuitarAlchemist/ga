namespace GA.Business.ProbabilisticGrammar.Tests;

using System.Linq;
using static GA.Business.ProbabilisticGrammar.WeightedMusicRuleModule;
using static GA.Business.ProbabilisticGrammar.WeightPersistence;

[TestFixture]
public class WeightPersistenceTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga_weights_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("GA_WEIGHTS_DIR", _tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("GA_WEIGHTS_DIR", null);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static Microsoft.FSharp.Collections.FSharpList<WeightedMusicRule> MakeRules()
    {
        var none = Microsoft.FSharp.Core.FSharpOption<string>.None;
        return Microsoft.FSharp.Collections.ListModule.OfArray(new[]
        {
            create("r1", "I IV V I", MusicRuleSource.ChordGrammar, none),
            bayesianUpdate(create("r2", "ii V I", MusicRuleSource.ChordGrammar, none), true),
        });
    }

    [Test]
    public void Save_ShouldCreateWeightsFile()
    {
        var rules = MakeRules();
        var result = save("TestGrammar", rules);
        Assert.That(result.IsOk, Is.True);
        var path = Path.Combine(_tempDir, "TestGrammar.weights.json");
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void Load_AfterSave_ShouldRoundTripRules()
    {
        var original = MakeRules();
        save("TestGrammar", original);
        var loaded = load("TestGrammar");
        Assert.That(loaded.IsOk, Is.True);
        var loadedList = loaded.ResultValue;
        Assert.That(loadedList.Count(), Is.EqualTo(original.Count()));
        var r2 = loadedList.First(r => r.RuleId == "r2");
        Assert.That(r2.Alpha, Is.EqualTo(2.0).Within(1e-9));
    }

    [Test]
    public void Load_MissingFile_ShouldReturnEmptyList()
    {
        var result = load("NonExistentGrammar");
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue.Count(), Is.EqualTo(0));
    }

    [Test]
    public void ListGrammars_ShouldReturnSavedGrammarNames()
    {
        var rules = MakeRules();
        save("ChordProgression", rules);
        save("ScaleTransformation", rules);
        var names = listGrammars();
        Assert.That(names, Does.Contain("ChordProgression"));
        Assert.That(names, Does.Contain("ScaleTransformation"));
    }

    [Test]
    public void Delete_ShouldRemoveFile()
    {
        save("DeleteMe", MakeRules());
        delete("DeleteMe");
        var result = load("DeleteMe");
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue.Count(), Is.EqualTo(0));
    }

    [Test]
    public void LoadOrInit_WhenNoFile_ShouldReturnDefaults()
    {
        var defaults = MakeRules();
        var result = loadOrInit("MissingGrammar", defaults);
        Assert.That(result.Count(), Is.EqualTo(defaults.Count()));
    }
}
