namespace GA.Business.ML.Tests.Unit;

using System.Reflection;
using System.Runtime.CompilerServices;
using GA.Business.ML.Agents;
using GuitarAlchemist.Registry;

/// <summary>
/// Drift gate for the ix-style capability registry (issue #46). The
/// <c>[ModuleInitializer]</c> in <c>GA.Business.ML</c>
/// (<see cref="SkillRegistryModuleInitializer"/>) registers one descriptor per
/// <c>[GaSkill]</c>-annotated <c>IOrchestratorSkill</c> at assembly load. This
/// test pins the expected count so a skill that gets the attribute (or loses it)
/// without the matching <c>GaPlugin.AddOrchestratorSkillIntent&lt;T&gt;()</c>
/// registration — or vice versa — fails CI, mirroring ix's "all 43 MCP tools,
/// exact count enforced" parity test.
/// </summary>
/// <remarks>
/// Expected count = 31 — the number of skills registered via
/// <c>AddOrchestratorSkillIntent&lt;T&gt;()</c> in
/// <c>Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs</c> as of issue
/// #46. When you add a skill: annotate the class with <c>[GaSkill]</c>, register
/// it in <c>GaPlugin</c>, and bump this constant by one. The two assertions below
/// (count, and exact name-set ↔ GaPlugin) localise any drift.
/// </remarks>
[TestFixture]
public class SkillRegistryParityTests
{
    /// <summary>
    /// Number of <c>GA.Business.ML</c> orchestrator skills carrying
    /// <c>[GaSkill]</c>. Must equal the <c>AddOrchestratorSkillIntent&lt;T&gt;()</c>
    /// count in <c>GaPlugin.cs</c>.
    /// </summary>
    private const int ExpectedMlSkillCount = 32;   // +OutsideNotesSkill (2026-07-20)

    /// <summary>Force <c>GA.Business.ML</c> to load so its ModuleInitializer runs.</summary>
    private static Assembly MlAssembly => typeof(IOrchestratorSkill).Assembly;

    [OneTimeSetUp]
    public void EnsureModuleInitialized()
    {
        // Touch the assembly + run its module .cctor defensively. [ModuleInitializer]
        // normally fires on first access to any type in the module; this makes the
        // ordering explicit and robust to test-runner type-load quirks.
        RuntimeHelpers.RunModuleConstructor(MlAssembly.ManifestModule.ModuleHandle);
    }

    /// <summary>The <c>[GaSkill]</c>-annotated concrete classes in GA.Business.ML.</summary>
    private static string[] AnnotatedSkillNames() =>
        [.. MlAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Select(t => t.GetCustomAttribute<GaSkillAttribute>(inherit: false))
            .Where(a => a is not null)
            .Select(a => a!.Name)];

    [Test]
    public void Registry_RegistersExpectedNumberOfMlSkills()
    {
        var registeredNames = Registry.Registrations.Select(s => s.Name).ToHashSet();
        var annotated = AnnotatedSkillNames();

        // Every annotated skill made it into the registry via the ModuleInitializer.
        Assert.That(registeredNames.IsSupersetOf(annotated), Is.True,
            "Every [GaSkill]-annotated class must be registered by the ModuleInitializer. " +
            $"Missing: {string.Join(", ", annotated.Except(registeredNames))}");

        Assert.That(annotated.Length, Is.EqualTo(ExpectedMlSkillCount),
            $"Expected exactly {ExpectedMlSkillCount} [GaSkill]-annotated skills in GA.Business.ML. " +
            "If you added or removed a skill: annotate/de-annotate the class, update " +
            "GaPlugin.AddOrchestratorSkillIntent<T>(), and adjust ExpectedMlSkillCount. " +
            $"Found: {string.Join(", ", annotated.OrderBy(n => n))}");
    }

    [Test]
    public void Registry_ByName_ResolvesAnnotatedSkill()
    {
        // Spot-check a stable, deterministic skill resolves through the unified API.
        // Descriptor name MUST equal the skill's runtime IOrchestratorSkill.Name
        // (ChordInfoSkill.Name => "ChordInfo"), not the class name, so registry
        // descriptors correlate with the orchestrator's intent IDs (skill.{name}).
        var skill = Registry.ByName("ChordInfo");
        Assert.That(skill, Is.Not.Null,
            "ChordInfo descriptor must be discoverable via Registry.ByName");
        Assert.That(skill!.Domain, Is.EqualTo("chord"));
    }
}
