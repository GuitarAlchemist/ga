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
    public void FlatFactories_BuildTheFlattenedNote()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Pitch.Flat.DFlat(4).PitchClass.Value, Is.EqualTo(1));
            Assert.That(Pitch.Flat.FFlat(4).PitchClass.Value, Is.EqualTo(4));
            Assert.That(Pitch.Flat.GFlat(4).PitchClass.Value, Is.EqualTo(6));
            Assert.That(Pitch.Flat.EFlat(4).PitchClass.Value, Is.EqualTo(3));
            Assert.That(Pitch.Flat.BFlat(4).PitchClass.Value, Is.EqualTo(10));
        });
    }
}
