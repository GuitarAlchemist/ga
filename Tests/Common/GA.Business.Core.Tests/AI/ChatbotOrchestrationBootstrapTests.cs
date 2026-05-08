namespace GA.Business.Core.Tests.AI;

using GA.Business.Core.Orchestration.Extensions;
using GA.Business.ML.Agents.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GaClosureRegistry = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;

/// <summary>
/// Regression test for the 2026-05-07 bootstrap miss: the F# closure
/// registry was never initialised at app startup, so Path B SKILL.md
/// skills (transpose / common-tones / diatonic-chords) returned
/// "closure not exposed" apologies on the live demo. The fix landed
/// <see cref="ChatbotOrchestrationExtensions.AddChatbotOrchestration"/>
/// the responsibility of calling <c>GaClosureBootstrap.init()</c>; this
/// fixture is the test that would have caught the regression had it
/// existed.
/// </summary>
/// <remarks>
/// Codex CLI second-opinion review explicitly called out that the
/// existing <c>TransposeSkillTests</c> + <c>DslEvalMcpToolsTests</c>
/// fakes the chat client / manually calls <c>DomainClosures.register()</c>
/// in OneTimeSetUp, so they prove the tool works only after explicit
/// registration — they cannot catch a missing init() call from the
/// production wiring. This fixture intentionally does NOT touch the F#
/// modules manually; it wires the production composition root and
/// asserts post-conditions on <see cref="GaClosureRegistry.Global"/>.
///
/// Caveat about global state: <see cref="GaClosureRegistry.Global"/> is
/// a process-wide singleton and other test fixtures in the same
/// assembly may have already populated it via <c>register()</c> calls.
/// In that case this fixture passes "vacuously" (the canaries are
/// present because someone else registered them earlier). CI's fresh
/// process per assembly catches the bug; locally, run the fixture
/// alone via <c>--filter</c> to be sure:
///   dotnet test --filter "FullyQualifiedName~ChatbotOrchestrationBootstrap"
/// </remarks>
[TestFixture]
public class ChatbotOrchestrationBootstrapTests
{
    private static readonly string[] CanaryClosures =
    [
        "domain.transposeChord",
        "domain.commonTones",
        "domain.diatonicChords",
    ];

    private static IServiceCollection BuildServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddChatbotOrchestration();
        return services;
    }

    [Test]
    public void AddChatbotOrchestration_PopulatesGlobalClosureRegistryWithCanaries()
    {
        // Wiring the composition root MUST be enough to make Path B
        // canary closures visible on GaClosureRegistry.Global. If the
        // GaClosureBootstrap.init() call ever gets removed from
        // AddChatbotOrchestration (and no other touchpoint replaces
        // it), one or more of these TryGet calls will return None on a
        // fresh process and this test will fail.
        BuildServiceCollection();

        Assert.Multiple(() =>
        {
            foreach (var name in CanaryClosures)
            {
                var found = GaClosureRegistry.Global.TryGet(name);
                Assert.That(
                    Microsoft.FSharp.Core.FSharpOption<GA.Business.DSL.Closures.GaClosureRegistry.GaClosure>.get_IsSome(found),
                    Is.True,
                    $"canary closure {name} must be visible after AddChatbotOrchestration. " +
                    "Verify GaClosureBootstrap.init() is called inside AddChatbotOrchestration.");
            }
        });
    }

    [Test]
    public void DslEvalMcpTools_AfterBootstrap_ListsCanaryClosures()
    {
        // Belt to the suspenders above — the LLM doesn't call
        // GaClosureRegistry.Global directly; it goes through
        // DslEvalMcpTools.ListClosures(). Asserting the same canaries
        // surface there proves the path the chatbot actually uses.
        BuildServiceCollection();

        var tool = new DslEvalMcpTools();
        var visible = tool.ListClosures().Closures.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            foreach (var name in CanaryClosures)
            {
                Assert.That(visible, Does.Contain(name),
                    $"DslEvalMcpTools.ListClosures must surface {name} after AddChatbotOrchestration");
            }
        });
    }

    [Test]
    public void AddChatbotOrchestration_IsIdempotentAcrossCalls()
    {
        // Multi-host scenarios (GaApi + GaChatbot.Api in the same
        // process, e.g. test harnesses) call AddChatbotOrchestration
        // more than once. The bootstrap call must not throw or
        // re-register duplicates; F# RegisterAll uses
        // ConcurrentDictionary semantics that overwrite-on-conflict.
        Assert.DoesNotThrow(() =>
        {
            BuildServiceCollection();
            BuildServiceCollection();
            BuildServiceCollection();
        });

        // Registry still has the canaries after triple-init.
        foreach (var name in CanaryClosures)
        {
            var found = GaClosureRegistry.Global.TryGet(name);
            Assert.That(
                Microsoft.FSharp.Core.FSharpOption<GA.Business.DSL.Closures.GaClosureRegistry.GaClosure>.get_IsSome(found),
                Is.True,
                $"canary closure {name} must still be present after repeated bootstrap calls");
        }
    }
}
