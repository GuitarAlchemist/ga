#pragma warning disable SKEXP0001 // IOrchestratorSkill, IIntent and SkillMd carry the experimental marker.

namespace GaApi.Tests.Services;

using GA.Business.Core.Orchestration.Intents;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Skills;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Pins "registered skill means routable skill" on the composition GaApi boots.
/// Chat reaches a skill only through <see cref="SemanticIntentRouter"/>, which
/// scores <see cref="IIntent"/> registrations that declare example prompts. A
/// skill registered as a bare <see cref="IOrchestratorSkill"/> still appears in
/// <c>GET /api/chatbot/skills</c>, but no chat message can ever reach it.
/// </summary>
[TestFixture]
public class SkillRoutabilityTests
{
    /// <summary>
    /// Skills that are registered on purpose without being routable, keyed by
    /// <see cref="IOrchestratorSkill.Name"/>, with the reason. Empty today.
    /// </summary>
    private static readonly Dictionary<string, string> UnroutableSkills = new(StringComparer.Ordinal);

    /// <summary>
    /// SKILL.md files with triggers that intentionally have no routed C# wrapper,
    /// keyed by folder-level <c>name</c>, with the reason.
    /// </summary>
    private static readonly Dictionary<string, string> SkillMdWithoutWrapper = new(StringComparer.Ordinal)
    {
        ["qa-architect"] = "Contributor guide for AI collaborators working with the QA Architect agent, not a user chat capability.",
    };

    private TestWebApplicationFactory? _factory;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new TestWebApplicationFactory();
        _factory.EnsureStarted();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory?.Dispose();

    [Test]
    public void EveryRegisteredSkill_HasAnIntentTheSemanticRouterCanScore()
    {
        using var scope = _factory!.Services.CreateScope();
        var skills = scope.ServiceProvider.GetServices<IOrchestratorSkill>().ToList();
        var routableIntentIds = scope.ServiceProvider.GetServices<IIntent>()
            .Where(i => i.ExamplePrompts.Count > 0)
            .Select(i => i.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(skills, Is.Not.Empty, "GaApi registered no IOrchestratorSkill at all.");

        var unreachable = skills
            .Where(s => !UnroutableSkills.ContainsKey(s.Name))
            .Where(s => !routableIntentIds.Contains(new OrchestratorSkillIntent(s).Id))
            .Select(s => $"{s.Name} ({s.GetType().Name})")
            .ToList();

        Assert.That(unreachable, Is.Empty,
            "These skills are registered as IOrchestratorSkill but chat cannot reach them: " +
            "register them with AddOrchestratorSkillIntent<T>() and give them ExamplePrompts, " +
            "or add them to UnroutableSkills with a reason.");
    }

    // Open questions ("Why does a minor chord sound sad?") need a routing target of their own,
    // or the nearest skill claims them and answers a different question.
    [Test]
    public void OpenQuestionIntent_IsScoredByTheSemanticRouter()
    {
        using var scope = _factory!.Services.CreateScope();
        var intent = scope.ServiceProvider.GetServices<IIntent>()
            .SingleOrDefault(i => i.Id == OpenQuestionIntent.IntentId);

        Assert.That(intent?.ExamplePrompts, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void EverySkillMdWithTriggers_IsServedByARoutedWrapper()
    {
        // SKILL.md bodies reach chat through C# wrappers that own the routing
        // metadata (SkillMdDrivenWrapperBase, CatalogSkillBase, ...). A SKILL.md
        // with no routed wrapper is dead text as far as the chatbot is concerned.
        var skillMds = SkillMdLoader.LoadFromDirectory(TestPaths.RepositoryPath("skills"));
        Assert.That(skillMds, Is.Not.Empty, "No SKILL.md with triggers found under skills/.");

        using var scope = _factory!.Services.CreateScope();
        var routedNames = scope.ServiceProvider.GetServices<IIntent>()
            .Where(i => i.ExamplePrompts.Count > 0)
            .Select(i => Normalize(i.Id["skill.".Length..]))
            .ToHashSet(StringComparer.Ordinal);

        var orphans = skillMds
            .Where(md => !SkillMdWithoutWrapper.ContainsKey(md.Name))
            .Where(md => !routedNames.Contains(Normalize(md.Name)))
            .Select(md => md.Name)
            .ToList();

        Assert.That(orphans, Is.Empty,
            "These SKILL.md files have no routed C# wrapper, so chat cannot reach them: " +
            "add a wrapper registered with AddOrchestratorSkillIntent<T>(), " +
            "or add them to SkillMdWithoutWrapper with a reason.");
    }

    private static string Normalize(string name) =>
        new([.. name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
