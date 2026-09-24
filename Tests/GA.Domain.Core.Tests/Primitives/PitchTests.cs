namespace GA.Domain.Core.Tests.Primitives;

using System.Reflection;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using NUnit.Framework;

/// <summary>
///     Tests for the well-known <see cref="Pitch.Sharp" /> / <see cref="Pitch.Flat" /> factories.
/// </summary>
[TestFixture]
public class PitchTests
{
    private static readonly Regex _wellKnownName = new("^(?<note>[A-G])(?<acc>Sharp|Flat)?(?<octave>[0-9])?$");

    private static IEnumerable<TestCaseData> WellKnownProperties() =>
        new[] { typeof(Pitch.Sharp), typeof(Pitch.Flat) }
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == type && _wellKnownName.Match(p.Name) is { Success: true } m && m.Groups["octave"].Success)
                .Select(p => new TestCaseData(type, p.Name).SetName($"{type.Name}.{p.Name}")));

    private static IEnumerable<TestCaseData> WellKnownFactories() =>
        new[] { typeof(Pitch.Sharp), typeof(Pitch.Flat) }
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType == type
                            && m.GetParameters() is [{ ParameterType: var t }] && t == typeof(Octave)
                            && _wellKnownName.IsMatch(m.Name))
                .Select(m => new TestCaseData(type, m.Name).SetName($"{type.Name}.{m.Name}(octave)")));

    private static string ExpectedName(string memberName, int octave)
    {
        var match = _wellKnownName.Match(memberName);
        var accidental = match.Groups["acc"].Value switch
        {
            "Sharp" => "#",
            "Flat" => "b",
            _ => string.Empty
        };
        return $"{match.Groups["note"].Value}{accidental}{octave}";
    }

    [TestCaseSource(nameof(WellKnownProperties))]
    public void WellKnownPitchProperty_MatchesItsName(Type type, string propertyName)
    {
        var pitch = (Pitch)type.GetProperty(propertyName)!.GetValue(null)!;
        var octave = int.Parse(propertyName[^1..]);

        Assert.That(pitch.ToString(), Is.EqualTo(ExpectedName(propertyName, octave)));
    }

    [TestCaseSource(nameof(WellKnownFactories))]
    public void WellKnownPitchFactory_MatchesItsName(Type type, string methodName)
    {
        foreach (var octave in new[] { 0, 4, 9 })
        {
            var pitch = (Pitch)type.GetMethod(methodName, [typeof(Octave)])!.Invoke(null, [(Octave)octave])!;

            Assert.That(pitch.ToString(), Is.EqualTo(ExpectedName(methodName, octave)), $"{type.Name}.{methodName}({octave})");
        }
    }

    [Test]
    public void FlatFactories_BuildTheFlattenedNote() =>
        Assert.Multiple(() =>
        {
            Assert.That(Pitch.Flat.DFlat(4).PitchClass.Value, Is.EqualTo(1));
            Assert.That(Pitch.Flat.FFlat(4).PitchClass.Value, Is.EqualTo(4));
            Assert.That(Pitch.Flat.GFlat(4).PitchClass.Value, Is.EqualTo(6));
            Assert.That(Pitch.Flat.EFlat(4).PitchClass.Value, Is.EqualTo(3));
            Assert.That(Pitch.Flat.BFlat(4).PitchClass.Value, Is.EqualTo(10));
        });

    [TestCase("C4", "C4")]
    [TestCase("c4", "C4")]
    [TestCase("C#4", "C#4")]
    [TestCase("F#-1", "F#-1")]
    [TestCase("G9", "G9")]
    public void SharpPitch_TryParse_Valid(string input, string expected)
    {
        var parsed = Pitch.Sharp.TryParse(input, null, out var pitch);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(pitch.ToString(), Is.EqualTo(expected));
        });
    }

    [TestCase("Eb2")] // flat spelling: must not be read as "b2"
    [TestCase("Bb3")]
    [TestCase("H4")]
    [TestCase("C")]
    [TestCase("C#")]
    [TestCase("C##4")]
    [TestCase("C4 ")]
    [TestCase("xC4")]
    [TestCase("C44")]
    [TestCase("C10")] // octave out of range (-1..9)
    public void SharpPitch_TryParse_Invalid(string input) =>
        Assert.That(Pitch.Sharp.TryParse(input, null, out _), Is.False);

    [TestCase("C4", "C4")]
    [TestCase("Bb3", "Bb3")]
    [TestCase("bb3", "Bb3")]
    [TestCase("Eb2", "Eb2")]
    [TestCase("Db-1", "Db-1")]
    public void FlatPitch_TryParse_Valid(string input, string expected)
    {
        var parsed = Pitch.Flat.TryParse(input, null, out var pitch);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(pitch.ToString(), Is.EqualTo(expected));
        });
    }

    [TestCase("C#4")]
    [TestCase("Ebb")]
    [TestCase("AEb2")]
    [TestCase("Bbbb3")]
    [TestCase("B11")]
    public void FlatPitch_TryParse_Invalid(string input) =>
        Assert.That(Pitch.Flat.TryParse(input, null, out _), Is.False);

    [Test]
    public void FlatPitch_Parse_KeepsTheAccidental() =>
        Assert.That(Pitch.Flat.Parse("Bb3").PitchClass.Value, Is.EqualTo(10));
}
