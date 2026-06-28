namespace GA.Business.ML.Agents;

using System.Reflection;
using System.Runtime.CompilerServices;
using GuitarAlchemist.Registry;

/// <summary>
/// Link-time skill registration for the <c>GA.Business.ML</c> assembly — the C#
/// analogue of ix's <c>#[ix_skill]</c> + <c>linkme::distributed_slice</c>
/// collection (issue #46).
/// </summary>
/// <remarks>
/// At assembly load the CLR runs <see cref="Init"/> (via
/// <see cref="ModuleInitializerAttribute"/>). It reflects over this assembly for
/// classes carrying <see cref="GaSkillAttribute"/> and pushes a descriptor for
/// each into <see cref="Registry"/>. This makes the orchestrator skill surface
/// discoverable through the unified <c>Registry.All</c> / <c>Registry.ByName</c>
/// API without booting the DI container — so tooling (CLI, MCP <c>tools/list</c>,
/// the federation <c>capability-registry.json</c>) and the drift-catching parity
/// test can enumerate skills the same way ix does.
/// <para>
/// This is additive: it registers <i>descriptors</i> only. The skills are still
/// constructed and dispatched through the existing DI registration in
/// <c>GaPlugin</c> (<c>AddOrchestratorSkillIntent&lt;TSkill&gt;()</c>) and the
/// <c>IOrchestratorSkill.ExecuteAsync</c> pipeline — re-sourcing the
/// orchestrator's skill enumeration from the registry is a deliberately separate,
/// follow-up step (issue #46 scope item 3).
/// </para>
/// </remarks>
internal static class SkillRegistryModuleInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        var assembly = typeof(SkillRegistryModuleInitializer).Assembly;

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = [.. ex.Types.Where(t => t is not null).Cast<Type>()];
        }

        foreach (var type in types)
        {
            if (!type.IsClass || type.IsAbstract) continue;

            var attr = type.GetCustomAttribute<GaSkillAttribute>(inherit: false);
            if (attr is null) continue;

            Registry.RegisterSkill(
                name: attr.Name,
                domain: attr.Domain,
                description: attr.Description,
                governanceTags: attr.GovernanceTags);
        }
    }
}
