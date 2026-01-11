namespace GA.Business.Core.Tests.Fretboard.Voicings.Search;

using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Fretboard.Voicings.Search;
using NUnit.Framework;

[TestFixture]
public class SemanticSearchTests
{
    private EnhancedVoicingSearchService SearchService => SemanticSearchTestFixture.SearchService;

    private async Task<IEnumerable<VoicingSearchResult>> SearchAndLog(string query)
    {
        TestContext.Progress.WriteLine($"\n[SEARCH] Query: \"{query}\"");
        var results = await SearchService.SearchAsync(query, t => Task.FromResult(SemanticSearchTestFixture.MockEmbed(t)));
        
        if (results.Any())
        {
            var top = results.First();
            TestContext.Progress.WriteLine($"[RESULT] Top: \"{top.Document.ChordName}\" | Score: {top.Score:F4} | Tags: [{string.Join(", ", top.Document.SemanticTags)}]");
        }
        else
        {
            TestContext.Progress.WriteLine("[RESULT] No results found.");
        }
        return results;
    }

    [Test]
    public async Task Search_SadChords_ReturnsMinorOrDiminished()
    {
        // "sad chords" is a classic semantic query. 
        // We expect minor chords, minor7, dim, or "sad" tag.
        
        var results = await SearchAndLog("sad chords");
        
        // Assert
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        var isSad = top.Document.SemanticTags.Intersect(new[] { "sad", "melancholy", "minor" }).Any();
        
        Assert.That(isSad, Is.True, $"Result '{top.Document.ChordName}' should be semantically 'sad'");
    }

    [Test]
    public async Task Search_JamesBond_ReturnsMinorMajor9()
    {
        var results = await SearchAndLog("james bond spy chord");
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        Assert.That(top.Document.ChordName, Does.Contain("Minor Major 9").Or.Contain("mMaj9"));
        Assert.That(top.Document.SemanticTags, Does.Contain("james-bond-chord"));
    }

    [Test]
    public async Task Search_MuMajor_ReturnsAdd9()
    {
        var results = await SearchAndLog("mu major steely dan");
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        Assert.That(top.Document.ChordName, Does.Contain("Add 9").Or.Contain("add9"));
        Assert.That(top.Document.SemanticTags, Does.Contain("mu-major"));
    }

    [Test]
    public async Task Search_NeoSoul_ReturnsMinor9()
    {
        var results = await SearchAndLog("neo soul r&b guitar");
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        Assert.That(top.Document.ChordName, Does.Contain("Minor 9").Or.Contain("m9"));
        Assert.That(top.Document.SemanticTags, Does.Contain("neo-soul"));
    }

    [Test]
    public async Task Search_Flamenco_ReturnsPhrygian()
    {
        var results = await SearchAndLog("flamenco spanish guitar");
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        Assert.That(top.Document.SemanticTags, Does.Contain("flamenco").Or.Contain("phrygian"));
    }

    [Test]
    public async Task Search_SoWhat_ReturnsQuartal()
    {
        var results = await SearchAndLog("so what chord modal");
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        Assert.That(top.Document.SemanticTags, Does.Contain("so-what-chord").Or.Contain("quartal"));
        Assert.That(top.Document.ChordName, Does.Contain("11")); // m11 is typical for So What
    }

    [Test]
    public async Task Search_Dreamy_ReturnsLydian()
    {
        var results = await SearchAndLog("dreamy wonder chords");
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        Assert.That(top.Document.SemanticTags, Does.Contain("dreamy"));
        Assert.That(top.Document.ChordName, Does.Contain("7#11").Or.Contain("Maj7"));
    }

    [Test]
    public async Task Search_Tense_ReturnsDiminished()
    {
        var results = await SearchAndLog("tense dissonant chord");
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        Assert.That(top.Document.SemanticTags, Does.Contain("tense").Or.Contain("diminished"));
        Assert.That(top.Document.ChordName, Does.Contain("Diminished").Or.Contain("dim"));
    }

    // -- Phase 2 Tests --

    [Test]
    public async Task Search_Aggressive_ReturnsPowerChords()
    {
        var results = await SearchAndLog("aggressive power chords");
        Assert.That(results.First().Document.ChordName, Is.EqualTo("E5 Power Chord"));
    }

    [Test]
    public async Task Search_LowRegister_ReturnsDeepChords()
    {
        var results = await SearchAndLog("deep heavy low chords");
        Assert.That(results.First().Document.ChordName, Is.EqualTo("Low E Major"));
    }


    [Test]
    public async Task Search_ShellVoicing_ReturnsGuideTones()
    {
        var results = await SearchAndLog("shell voicings guide tones");
        Assert.That(results.First().Document.ChordName, Is.EqualTo("C Maj7 Shell"));
    }

    [Test]
    public async Task Search_Rootless_ReturnsRootlessVoicing()
    {
        var results = await SearchAndLog("rootless jazz comping");
        Assert.That(results.First().Document.ChordName, Is.EqualTo("C Maj9 Rootless"));
    }

    [Test]
    public async Task Search_Dense_ReturnsCluster()
    {
        var results = await SearchAndLog("dense cluster chords");
        Assert.That(results.First().Document.ChordName, Is.EqualTo("C Add2 Closed"));
    }

    [Test]
    public async Task Search_Stable_ReturnsMajorTriad()
    {
        var results = await SearchAndLog("stable grounded chords");
        Assert.That(results.First().Document.ChordName, Is.EqualTo("C Major Triad"));
    }

    [Test]
    public async Task Search_Bright_ReturnsLydian()
    {
        var results = await SearchAndLog("bright lydian chords");
        Assert.That(results.First().Document.ChordName, Is.EqualTo("C Lydian"));
    }

    [Test]
    public async Task Search_BarreChords_ReturnsBarreVoicings()
    {
        // Act
        var results = await SearchAndLog("barre chords");
        
        // Assert
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        
        Assert.That(top.Document.Position, Is.EqualTo("Barre"), $"Result '{top.Document.ChordName}' should be a barre chord");
        Assert.That(top.Document.SemanticTags, Does.Contain("barre"));
    }

    [Test]
    public async Task Search_CommonChords_ReturnsTriads()
    {
        // Act
        // "common" maps to index 6
        var results = await SearchAndLog("common chords");
        
        // Assert
        Assert.That(results, Is.Not.Empty);
        
        var top = results.First();
        Assert.That(top.Document.SemanticTags, Does.Contain("common"));
        // Check if top is one of our common chords (G, D, Em)
        var commonNames = new[] { "G Major", "D Major", "E Minor", "C Major" }; // C Major in test set might not be tagged common? Let's check G/D/Em
        Assert.That(commonNames, Does.Contain(top.Document.ChordName));
    }

    [Test]
    public async Task Search_SadOpenChords_ReturnsCombinedProperties()
    {
        // Act
        // "sad" (idx 0) + "open" (idx 4)
        var results = await SearchAndLog("sad open chords");
        
        // Assert
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        
        // Should favor Em (Sad + Open) over Bm (Sad + Barre) or G (Happy + Open)
        Assert.That(top.Document.ChordName, Is.EqualTo("E Minor"));
        Assert.That(top.Document.Position, Is.EqualTo("Open"));
        Assert.That(top.Document.SemanticTags, Does.Contain("sad"));
    }

    [Test]
    public async Task Search_HendrixChord_Returns7Shar9()
    {
        // Act
        var results = await SearchAndLog("hendrix chord");

        // Debug
        TestContext.Progress.WriteLine("\nResults for 'hendrix chord':");
        foreach(var r in results) TestContext.Progress.WriteLine($" - {r.Document.ChordName}: {r.Score}");

        // Assert
        Assert.That(results, Is.Not.Empty);
        var top = results.First();
        
        Assert.That(top.Document.ChordName, Contains.Substring("7#9"));
        Assert.That(top.Document.SemanticTags, Contains.Item("hendrix"));
    }

    [Test]
    public async Task Search_OpenChords_ReturnsOpenPosition()
    {
        // Act
        var results = await SearchAndLog("open chords");
        
        // Debug
        TestContext.Progress.WriteLine("\nResults for 'open chords':");
        foreach(var r in results) TestContext.Progress.WriteLine($" - {r.Document.ChordName}: {r.Score}");

        // Assert
        Assert.That(results, Is.Not.Empty);
        var result = results.First();
        
        Assert.That(result.Document.Position, Is.EqualTo("Open"));
        Assert.That(result.Document.Difficulty == "Beginner", Is.True, $"Result '{result.Document.Id}' should be beginner difficulty");
    }
    
    [Test]
    public async Task Search_JazzyChords_ReturnsComplexHarmony()
    {
        // Act
        var results = await SearchAndLog("jazzy chords");
        
        // Debug
        // TestContext.Progress.WriteLine("\nResults for 'jazzy chords':");
        // foreach(var r in results) TestContext.Progress.WriteLine($" - {r.Document.ChordName}: {r.Score}");

        Assert.That(results, Is.Not.Empty);
        var result = results.First();
        
        // Force fail to see score
        if (result.Document.ChordName == "G Major")
        {
             var c13 = results.FirstOrDefault(r => r.Document.ChordName.Contains("C 13"));
             Assert.Fail($"Top: {result.Document.ChordName} ({result.Score}). C13: {c13?.Document.ChordName} ({c13?.Score})");
        }
        
        var isJazzy = result.Document.SemanticTags.Intersect(new[] { "jazz", "complex", "dominant" }).Any();
        Assert.That(isJazzy, Is.True, $"Result '{result.Document.ChordName}' should be complex/jazzy");
    }

    [Test]
    public async Task Search_HighRegister_ReturnsSparklingChords()
    {
        // Use 'high register' terminology to map to the mock embedding correctly
        var results = await SearchAndLog("high register upper position");
        Assert.That(results.First().Document.ChordName, Is.EqualTo("High G Major"));
    }
}
