namespace GA.Business.ML.Tests.Unit;

using System.Text.RegularExpressions;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Skills;
using GA.Domain.Core.Theory.Tonal;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Binds the theory tables that catalog skills reproduce verbatim to the domain, so a wrong row
/// fails here instead of reaching users. The 2026-10-03 probe found the circle-of-fifths table
/// calling Cb major "≡ C# major" (it is B major).
/// </summary>
[TestFixture]
public sealed class SkillMdTheoryTests
{
    private static readonly Regex KeyRow = new(
        @"^\|\s*(?<pos>[^|]+?)\s*\|\s*(?<key>[A-G][#b]?) major\s*\|\s*(?<sig>[^|]+?)\s*\|",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex ModeRow = new(
        @"^\|\s*\d\s*\|\s*\*\*(?<mode>\w+)\*\*\s*\|\s*`(?<formula>[^`]+)`",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static int PitchClassMask(Key key) =>
        key.Notes.Aggregate(0, (acc, n) => acc | (1 << n.PitchClass.Value));

    [Test]
    public void CircleOfFifths_KeySignaturesAndEnharmonicsMatchTheDomain()
    {
        var body = CatalogSkillMdLoader.LoadBodyOrFallback("circle-of-fifths", string.Empty);
        var rows = KeyRow.Matches(body);

        Assert.That(rows, Has.Count.EqualTo(15), "8 sharp-side and 7 flat-side major keys");
        foreach (Match row in rows)
        {
            var key = KeyNaming.ResolveKey(row.Groups["key"].Value, isMinor: false);
            Assert.That(key, Is.Not.Null, row.Value);

            var signature = row.Groups["sig"].Value;
            var accidentals = signature == "(none)" ? 0 : Regex.Matches(signature, "[A-G][#b]").Count;
            Assert.That(accidentals, Is.EqualTo(key!.KeySignature.AccidentalCount), row.Value);

            var enharmonic = Regex.Match(row.Groups["pos"].Value, @"≡ (?<other>[A-G][#b]?) major");
            if (!enharmonic.Success) continue;
            var other = KeyNaming.ResolveKey(enharmonic.Groups["other"].Value, isMinor: false);
            Assert.That(other, Is.Not.Null, row.Value);
            Assert.That(PitchClassMask(other!), Is.EqualTo(PitchClassMask(key)), row.Value);
        }
    }

    [Test]
    public async Task Modes_CatalogFormulasMatchModesYaml()
    {
        var body = CatalogSkillMdLoader.LoadBodyOrFallback("modes", string.Empty);
        var rows = ModeRow.Matches(body);
        var skill = new ModesSkill(NullLogger<ModesSkill>.Instance);

        Assert.That(rows, Has.Count.EqualTo(7));
        foreach (Match row in rows)
        {
            var answer = await skill.ExecuteAsync($"What is {row.Groups["mode"].Value}");
            Assert.That(answer.Result, Does.Contain($"(formula `{row.Groups["formula"].Value}`)"), row.Value);
        }
    }
}
