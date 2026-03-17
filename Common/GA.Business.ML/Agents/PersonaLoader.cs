namespace GA.Business.ML.Agents;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// A persona definition loaded from a Demerzel <c>.persona.yaml</c> file.
/// </summary>
public sealed record PersonaDefinition(
    string Name, string Role, string Domain,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Constraints,
    PersonaVoice Voice);

/// <summary>
/// Voice characteristics for a persona.
/// </summary>
public sealed record PersonaVoice(string Tone, string Verbosity, string Style);

/// <summary>
/// Loads Demerzel persona definitions from <c>governance/demerzel/personas/</c>
/// and maps them to GA agent IDs.
/// </summary>
public sealed class PersonaLoader(ILogger<PersonaLoader> logger)
{
    private static readonly Dictionary<string, string> AgentPersonaMap = new()
    {
        [AgentIds.Critic]   = "skeptical-auditor",
        [AgentIds.Composer] = "kaizen-optimizer",
        [AgentIds.Theory]   = "reflective-architect",
    };

    private readonly ConcurrentDictionary<string, PersonaDefinition?> _cache = new();

    /// <summary>
    /// Gets the persona mapped to the given agent ID, or null if none exists.
    /// </summary>
    public PersonaDefinition? GetPersona(string agentId)
    {
        if (!AgentPersonaMap.TryGetValue(agentId, out var personaName))
            return null;

        return _cache.GetOrAdd(personaName, name => LoadPersona(name));
    }

    /// <summary>
    /// Builds a formatted persona context block for injection into a system prompt.
    /// Returns null if no persona is mapped for the given agent.
    /// </summary>
    public string? BuildPersonaContext(string agentId)
    {
        var persona = GetPersona(agentId);
        if (persona is null) return null;

        return $"""
            [Persona: {persona.Name}]
            Role: {persona.Role}
            Domain: {persona.Domain}
            Capabilities: {string.Join(", ", persona.Capabilities)}
            Constraints: {string.Join(", ", persona.Constraints)}
            Voice: tone={persona.Voice.Tone}, verbosity={persona.Voice.Verbosity}, style={persona.Voice.Style}
            """;
    }

    private PersonaDefinition? LoadPersona(string personaName)
    {
        try
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot is null) return null;

            var path = Path.Combine(repoRoot, "governance", "demerzel", "personas", $"{personaName}.persona.yaml");
            if (!File.Exists(path))
            {
                logger.LogDebug("PersonaLoader: persona file not found at {Path}", path);
                return null;
            }

            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var raw = deserializer.Deserialize<PersonaYaml>(yaml);
            if (raw is null) return null;

            var voice = raw.Voice is not null
                ? new PersonaVoice(
                    raw.Voice.TryGetValue("tone", out var t) ? t : "neutral",
                    raw.Voice.TryGetValue("verbosity", out var v) ? v : "normal",
                    raw.Voice.TryGetValue("style", out var s) ? s : "default")
                : new PersonaVoice("neutral", "normal", "default");

            logger.LogDebug("PersonaLoader: loaded persona {Name} from {Path}", raw.Name, path);
            return new PersonaDefinition(
                raw.Name ?? personaName,
                raw.Role ?? "unspecified",
                raw.Domain ?? "general",
                raw.Capabilities ?? [],
                raw.Constraints ?? [],
                voice);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PersonaLoader: failed to load persona {Name}", personaName);
            return null;
        }
    }

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "governance", "demerzel")))
                return dir;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        return null;
    }

    private sealed class PersonaYaml
    {
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? Domain { get; set; }
        public List<string>? Capabilities { get; set; }
        public List<string>? Constraints { get; set; }
        public Dictionary<string, string>? Voice { get; set; }
    }
}
