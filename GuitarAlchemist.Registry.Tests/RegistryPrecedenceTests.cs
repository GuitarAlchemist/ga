namespace GuitarAlchemist.Registry.Tests;

using System.Text.Json.Nodes;
using GuitarAlchemist.Registry;

/// <summary>
/// Pins the duplicate-name precedence rules of <see cref="Registry.RegisterSkill(GaSkill)"/>
/// documented for issue #46: last-write-wins among explicit registrations, and
/// explicit registrations win over reflection-discovered skills of the same name.
/// </summary>
/// <remarks>
/// Uses fixture-exclusive names so these tests neither collide with skills any
/// other assembly's <c>[ModuleInitializer]</c> registered into the process-wide
/// static <see cref="Registry"/>, nor mutate the <c>test.echo</c> skill that
/// <see cref="RegistryDiscoveryTests"/> asserts on (NUnit test order is not
/// guaranteed, so overriding a shared name would cause cross-fixture flake).
/// </remarks>
[TestFixture]
public class RegistryPrecedenceTests
{
    /// <summary>
    /// A reflection-discoverable skill exclusive to this fixture — its name is
    /// asserted nowhere else, so overriding it here is safe.
    /// </summary>
    [GaSkill("test.precedence.reflected", "reflected-default")]
    public static JsonNode Reflected(JsonNode input) => input;

    [Test]
    public void RegisterSkill_SameNameTwice_LastWriteWins()
    {
        const string name = "test.precedence.dup";

        Registry.RegisterSkill(name, domain: "first");
        Registry.RegisterSkill(name, domain: "second");

        var resolved = Registry.ByName(name);
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Domain, Is.EqualTo("second"),
            "ConcurrentDictionary indexer assignment is last-write-wins: the second " +
            "registration of the same name must replace the first.");

        // And there is exactly one entry under that name in the merged view.
        Assert.That(Registry.All.Count(s => s.Name == name), Is.EqualTo(1));
    }

    [Test]
    public void RegisterSkill_OverridesReflectionDiscoveredSkillOfSameName()
    {
        // "test.precedence.reflected" is a reflection-discovered skill (the
        // [GaSkill] static method on this fixture, domain "reflected-default").
        // An explicit registration of the same name must win in BOTH ByName and All.
        const string discoveredName = "test.precedence.reflected";

        // Sanity: before any explicit registration, the reflected default is what resolves.
        Assert.That(Registry.ByName(discoveredName)?.Domain,
            Is.EqualTo("reflected-default"),
            "precondition: the reflection-discovered skill resolves with its declared domain.");

        Registry.RegisterSkill(discoveredName, domain: "explicit-override");

        var resolved = Registry.ByName(discoveredName);
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Domain, Is.EqualTo("explicit-override"),
            "ByName must prefer the explicit registration over the reflection-discovered one.");

        var fromAll = Registry.All.Single(s => s.Name == discoveredName);
        Assert.That(fromAll.Domain, Is.EqualTo("explicit-override"),
            "All must overlay the explicit registration over reflection discovery (no duplicate, explicit wins).");
    }
}
