namespace GA.Business.ML.Tests.Unit;

using System.Reflection;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Memory;

/// <summary>
/// Static-analysis pin enforcing the architectural boundary around
/// <see cref="IOperatorTranscriptReader"/> — the interface that exposes
/// cross-session transcript reads. Chat-runtime hooks must NOT inject
/// it; if they did, an in-flight chat request could read other users'
/// transcripts and leak them into the calling user's prompt context.
/// </summary>
/// <remarks>
/// <para>
/// The boundary lives in the type name and DI registration, not in any
/// runtime check. This test makes the boundary enforceable: any reflection
/// that surfaces a chat-runtime <see cref="IChatHook"/> implementation
/// taking <see cref="IOperatorTranscriptReader"/> in its constructor will
/// fail here. The author of such a change is expected to either
/// (a) revert and use a per-session reader, or (b) delete this test with
/// an explicit justification commit message.
/// </para>
/// <para>
/// Reflection-load is intentional — we don't want to maintain a
/// hand-curated allowlist of hook types; new hooks are protected by this
/// pin from the moment they're registered as <see cref="IChatHook"/>.
/// </para>
/// </remarks>
[TestFixture]
public class IOperatorTranscriptReaderBoundaryTests
{
    [Test]
    public void NoChatHookConstructor_DependsOn_IOperatorTranscriptReader()
    {
        var hookInterface = typeof(IChatHook);
        var bannedDep = typeof(IOperatorTranscriptReader);

        // Scan the GA.Business.ML assembly for every concrete IChatHook
        // implementation. Allows future hooks added by the same plugin to
        // be covered automatically.
        var hookImpls = hookInterface.Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && hookInterface.IsAssignableFrom(t))
            .ToList();

        Assert.That(hookImpls, Is.Not.Empty,
            "Expected at least one IChatHook implementation in the assembly. " +
            "If this fails, the assembly load is broken (no hooks discovered).");

        var violations = new List<string>();
        foreach (var hookType in hookImpls)
        {
            foreach (var ctor in hookType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var p in ctor.GetParameters())
                {
                    if (p.ParameterType == bannedDep)
                    {
                        violations.Add(
                            $"{hookType.FullName}::ctor parameter '{p.Name}' has type " +
                            $"{bannedDep.Name} — IChatHook implementations must NOT depend on " +
                            "the operator-only transcript reader. Use a per-session reader, " +
                            "or file an issue if one doesn't exist yet.");
                    }
                }
            }
        }

        Assert.That(violations, Is.Empty,
            "Cross-session boundary violation:\n  " + string.Join("\n  ", violations));
    }
}
