namespace GuitarAlchemist.Registry;

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;

/// <summary>
/// Discovers and indexes <see cref="GaSkillAttribute"/>-tagged skills across all
/// loaded assemblies, and accepts explicit registrations pushed in by
/// <c>[ModuleInitializer]</c> hooks at assembly load.
/// </summary>
/// <remarks>
/// Two registration paths, both surfaced through the same <see cref="All"/> /
/// <see cref="ByName"/> API:
/// <list type="number">
///   <item>
///     <b>Reflection discovery</b> (original path, unchanged): scans
///     <c>AppDomain.CurrentDomain.GetAssemblies()</c> for public static methods
///     marked with <see cref="GaSkillAttribute"/>. For each match it builds a
///     <see cref="GaSkill"/> with a delegate Handler bound to the method.
///     Schema resolution: if a sibling method named <c>{MethodName}Schema</c>
///     exists with the signature <c>() =&gt; JsonObject</c>, it is used;
///     otherwise an empty <see cref="JsonObject"/> is returned.
///   </item>
///   <item>
///     <b>Explicit registration</b> (added for issue #46): callers — typically a
///     <c>[ModuleInitializer]</c> in a skill-bearing assembly, mirroring ix's
///     <c>#[ix_skill]</c> + <c>linkme::distributed_slice</c> link-time
///     collection — push descriptors via <see cref="RegisterSkill(GaSkill)"/>.
///     This is the path used for instance skills (e.g. DI-constructed
///     <c>IOrchestratorSkill</c> implementations) that cannot be modelled as a
///     static <c>JsonNode (JsonNode)</c> method.
///   </item>
/// </list>
/// Explicitly registered skills take precedence over reflection-discovered ones
/// with the same <see cref="GaSkill.Name"/>. The explicit store is a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>, so <see cref="RegisterSkill(GaSkill)"/>
/// is safe to call from multiple module initializers concurrently.
/// </remarks>
public static class Registry
{
    /// <summary>
    /// Explicitly registered skills, keyed by name. Populated at assembly load by
    /// <c>[ModuleInitializer]</c> hooks. Thread-safe.
    /// </summary>
    private static readonly ConcurrentDictionary<string, GaSkill> Registered =
        new(StringComparer.Ordinal);

    private static readonly Lazy<IReadOnlyDictionary<string, GaSkill>> DiscoveredIndex =
        new(BuildIndex);

    /// <summary>
    /// All skills, keyed by name — explicit registrations merged over reflection
    /// discovery (explicit wins on name collision).
    /// </summary>
    public static IEnumerable<GaSkill> All => Snapshot().Values;

    /// <summary>Look up a skill by name; returns null when absent.</summary>
    public static GaSkill? ByName(string name) =>
        Registered.TryGetValue(name, out var explicitSkill)
            ? explicitSkill
            : DiscoveredIndex.Value.TryGetValue(name, out var discovered) ? discovered : null;

    /// <summary>
    /// Explicitly register (or replace) a skill by name. Thread-safe; intended to
    /// be called from a <c>[ModuleInitializer]</c> at assembly load. Returns the
    /// stored descriptor. Idempotent on identical input.
    /// </summary>
    public static GaSkill RegisterSkill(GaSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        Registered[skill.Name] = skill;
        return skill;
    }

    /// <summary>
    /// Convenience overload — builds a descriptor-only <see cref="GaSkill"/> (no
    /// executable handler) and registers it. Used for instance skills whose body
    /// runs through their own pipeline (e.g. <c>IOrchestratorSkill.ExecuteAsync</c>)
    /// rather than the static <c>JsonNode (JsonNode)</c> handler. The default
    /// handler throws if invoked, signalling "descriptor only — dispatch through
    /// the owning pipeline, not the registry handler."
    /// </summary>
    public static GaSkill RegisterSkill(
        string name,
        string domain,
        string description = "",
        IReadOnlyList<string>? governanceTags = null) =>
        RegisterSkill(new GaSkill(
            Name: name,
            Domain: domain,
            Description: description,
            Handler: DescriptorOnlyHandler(name),
            Schema: static () => [],
            GovernanceTags: governanceTags ?? []));

    /// <summary>Skills contributed only by explicit registration. Thread-safe snapshot.</summary>
    public static IEnumerable<GaSkill> Registrations => Registered.Values.ToArray();

    private static IReadOnlyDictionary<string, GaSkill> Snapshot()
    {
        // Start from reflection discovery, then overlay explicit registrations.
        Dictionary<string, GaSkill> merged = new(StringComparer.Ordinal);
        foreach (var (name, skill) in DiscoveredIndex.Value)
        {
            merged[name] = skill;
        }
        foreach (var (name, skill) in Registered)
        {
            merged[name] = skill;
        }
        return merged;
    }

    private static Func<JsonNode, JsonNode> DescriptorOnlyHandler(string name) =>
        _ => throw new InvalidOperationException(
            $"Skill '{name}' is registered as a descriptor only; invoke it through " +
            "its owning pipeline (e.g. IOrchestratorSkill.ExecuteAsync), not the registry Handler.");

    private static IReadOnlyDictionary<string, GaSkill> BuildIndex()
    {
        Dictionary<string, GaSkill> dict = new(StringComparer.Ordinal);
        foreach (var skill in Discover())
        {
            dict[skill.Name] = skill;
        }
        return dict;
    }

    private static IEnumerable<GaSkill> Discover()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some assemblies (test hosts, dynamic ones) may fail to fully
                // load — keep the types we can read and skip the rest.
                types = [.. ex.Types.Where(t => t is not null).Cast<Type>()];
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                MethodInfo[] methods;
                try
                {
                    methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                }
                catch
                {
                    continue;
                }

                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<GaSkillAttribute>();
                    if (attr is null) continue;

                    var built = TryBuildSkill(attr, method);
                    if (built is not null) yield return built;
                }
            }
        }
    }

    private static GaSkill? TryBuildSkill(GaSkillAttribute attr, MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 1 ||
            !typeof(JsonNode).IsAssignableFrom(parameters[0].ParameterType) ||
            !typeof(JsonNode).IsAssignableFrom(method.ReturnType))
        {
            // Skills must have signature: static JsonNode (JsonNode input)
            return null;
        }

        Func<JsonNode, JsonNode> handler = input =>
        {
            var result = method.Invoke(null, [input]);
            return (JsonNode)result!;
        };

        Func<JsonObject> schema = ResolveSchema(method);

        return new GaSkill(
            Name: attr.Name,
            Domain: attr.Domain,
            Description: attr.Description,
            Handler: handler,
            Schema: schema,
            GovernanceTags: attr.GovernanceTags);
    }

    private static Func<JsonObject> ResolveSchema(MethodInfo method)
    {
        var schemaMethod = method.DeclaringType?.GetMethod(
            $"{method.Name}Schema",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (schemaMethod is not null && typeof(JsonObject).IsAssignableFrom(schemaMethod.ReturnType))
        {
            return () => (JsonObject)schemaMethod.Invoke(null, null)!;
        }

        return () => [];
    }
}
