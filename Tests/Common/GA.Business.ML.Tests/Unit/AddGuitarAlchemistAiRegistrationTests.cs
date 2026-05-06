namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>
/// Regression guard for the DI bootstrap that GaApi actually invokes.
/// <see cref="MlServiceCollectionExtensions.AddGuitarAlchemistAI"/> (uppercase
/// <c>AI</c>) and <see cref="AiServiceExtensions.AddGuitarAlchemistAi"/>
/// (lowercase <c>Ai</c>) drifted: the lowercase variant registered
/// <see cref="IChatClientFactory"/>; the uppercase one didn't. Once the
/// canonical <c>skills/</c> directory had real SKILL.md files,
/// <see cref="GA.Business.ML.Agents.Plugins.SkillMdPlugin"/> demanded the
/// factory inside <c>IEnumerable&lt;IOrchestratorSkill&gt;</c> resolution and
/// every chatbot endpoint started returning HTTP 500. This fixture pins the
/// requirement that <c>AddGuitarAlchemistAI</c> registers the factory.
/// </summary>
[TestFixture]
public class AddGuitarAlchemistAiRegistrationTests
{
    [Test]
    public void AddGuitarAlchemistAI_RegistersIChatClientFactory()
    {
        var services = new ServiceCollection();

        // DefaultChatClientFactory's ctor pulls IConfiguration + IChatClient —
        // provide stubs so the resolve doesn't fail on dependencies unrelated
        // to the assertion under test.
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IChatClient>(_ => new Mock<IChatClient>().Object);

        services.AddGuitarAlchemistAI();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IChatClientFactory>();

        Assert.That(factory, Is.Not.Null,
            "GaApi calls AddGuitarAlchemistAI (uppercase). If this throws, " +
            "every chatbot endpoint will 500 once SkillMdPlugin finds a " +
            "SKILL.md to register, because SkillMdDrivenSkill needs " +
            "IChatClientFactory.");
        Assert.That(factory, Is.InstanceOf<DefaultChatClientFactory>());
    }
}
