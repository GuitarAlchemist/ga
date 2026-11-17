namespace GA.Business.ML.Providers;

/// <summary>
/// Remembers how the most recent LLM generation ended, so the readiness probe
/// can report a provider that answers <c>/api/tags</c> but refuses to generate
/// (for example Ollama Cloud's "Add usage credits" error). Passive by design:
/// <c>/status</c> is public and polled after every answer, so it must not run
/// paid inference itself.
/// </summary>
public sealed class ChatGenerationHealth(TimeProvider? timeProvider = null)
{
    /// <summary>
    /// How long a recorded outcome speaks for the provider. Older outcomes are
    /// reported as unknown, so a failure does not keep <c>/status</c> red forever
    /// on a host that receives no traffic after the provider recovers.
    /// </summary>
    public static readonly TimeSpan OutcomeWindow = TimeSpan.FromMinutes(10);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private (DateTimeOffset At, bool Ok, string Diagnostic)? _last;

    public void RecordSuccess() => Record(true, "ok");

    public void RecordFailure(string diagnostic) => Record(false, diagnostic);

    public void RecordFailure(Exception exception) =>
        RecordFailure($"{exception.GetType().Name}: {exception.Message}");

    /// <summary>
    /// The latest outcome, or <see langword="null"/> when no generation ended
    /// within <see cref="OutcomeWindow"/>.
    /// </summary>
    public (bool Ok, string Diagnostic, TimeSpan Age)? Latest
    {
        get
        {
            lock (_gate)
            {
                if (_last is not { } last)
                {
                    return null;
                }

                var age = _time.GetUtcNow() - last.At;
                return age > OutcomeWindow ? null : (last.Ok, last.Diagnostic, age);
            }
        }
    }

    private void Record(bool ok, string diagnostic)
    {
        lock (_gate)
        {
            _last = (_time.GetUtcNow(), ok, diagnostic);
        }
    }
}
