namespace GuitarAlchemist.Registry;

using System.Reflection;
using System.Text.Json.Nodes;

/// <summary>
/// Discovers and indexes <see cref="GaSkillAttribute"/>-tagged methods across
/// all loaded assemblies. The result is cached on first access.
/// </summary>
/// <remarks>
/// Discovery scans <c>AppDomain.CurrentDomain.GetAssemblies()</c> for public
/// static methods marked with <see cref="GaSkillAttribute"/>. For each match
/// it builds a <see cref="GaSkill"/> with a delegate Handler bound to the
/// method. Schema resolution: if a sibling method named
/// <c>{MethodName}Schema</c> exists with the signature <c>() =&gt; JsonObject</c>,
/// it is used; otherwise an empty <see cref="JsonObject"/> is returned.
/// </remarks>
public static class Registry
{
    private static readonly Lazy<IReadOnlyDictionary<string, GaSkill>> Index =
        new(BuildIndex);

    /// <summary>All discovered skills, keyed by name.</summary>
    public static IEnumerable<GaSkill> All => Index.Value.Values;

    /// <summary>Look up a skill by name; returns null when absent.</summary>
    public static GaSkill? ByName(string name) =>
        Index.Value.TryGetValue(name, out var skill) ? skill : null;

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
