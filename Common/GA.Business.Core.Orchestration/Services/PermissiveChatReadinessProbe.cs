namespace GA.Business.Core.Orchestration.Services;

using GA.Business.Core.Orchestration.Abstractions;

/// <summary>
/// Default <see cref="IChatReadinessProbe"/> registered by
/// <c>AddChatbotOrchestration</c>. Always reports ready. The
/// <see cref="ReadinessGatedChatApplicationService"/> decorator still emits a
/// <c>readiness.check</c> trace step so observability is consistent across
/// hosts; hosts that want real gating override this binding with a richer
/// probe (e.g. provider reachability + model installed-list verification).
/// </summary>
public sealed class PermissiveChatReadinessProbe : IChatReadinessProbe
{
    private static readonly ChatReadinessResult Ready =
        new(IsReady: true, Reason: "permissive default — no host-specific probe registered");

    public Task<ChatReadinessResult> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Ready);
}
